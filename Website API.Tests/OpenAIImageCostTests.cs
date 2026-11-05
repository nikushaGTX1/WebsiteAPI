using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Website_API.Data;
using Website_API.Models;
using Website_API.Services;
using Xunit;

namespace Website_API.Tests;

public class OpenAIImageCostTests
{
    private const string Model = "gpt-image-2.5-sunburst";
    private const string Sample = """
        {"input_tokens":1554,"input_tokens_details":{"text_tokens":97,"image_tokens":1457},
         "output_tokens":196,"output_tokens_details":{"image_tokens":196,"text_tokens":0},"total_tokens":1750}
        """;
    [Fact]
    public void RealSavedUsageHasSeparateInputOutputAndTextCosts()
    {
        using var doc = JsonDocument.Parse(Sample);
        var cost = OpenAIImageCostCalculator.Calculate(Model, doc.RootElement)!;
        Assert.Equal(0.000485m, cost.TextInputUsd);
        Assert.Equal(0.011656m, cost.ImageInputUsd);
        Assert.Equal(0.005880m, cost.ImageOutputUsd);
        Assert.Equal(0.018021m, cost.TotalUsd);
        Assert.Equal(0.090105m, cost.TotalUsd * 5);
        Assert.Equal(1.8021m, cost.TotalUsd * 100);
        Assert.Equal(18.021m, cost.TotalUsd * 1000);
        Assert.Equal(90.105m, cost.TotalUsd * 5000);
        Assert.Equal(cost, OpenAIImageCostCalculator.Calculate("gpt-image-2.5-sunburst-2026-09-08", doc.RootElement));
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"input_tokens":0,"input_tokens_details":{"text_tokens":0,"image_tokens":0},"output_tokens":0}""")]
    [InlineData("""{"input_tokens":2,"input_tokens_details":{"text_tokens":1,"image_tokens":2},"output_tokens":5}""")]
    [InlineData("""{"input_tokens":3,"input_tokens_details":{"text_tokens":-1,"image_tokens":4},"output_tokens":5}""")]
    [InlineData("""{"input_tokens":3,"input_tokens_details":{"text_tokens":1,"image_tokens":2},"output_tokens":5,"total_tokens":999}""")]
    [InlineData("""{"input_tokens":3,"input_tokens_details":{"text_tokens":1,"image_tokens":2},"output_tokens":5,"output_tokens_details":{"text_tokens":1,"image_tokens":4}}""")]
    public void MissingMalformedOrInconsistentUsageCannotBecomeZeroSpend(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.Null(OpenAIImageCostCalculator.Calculate(Model, doc.RootElement));
        var metadata = JsonSerializer.Serialize(new { model = Model, usage = doc.RootElement });
        var result = OpenAIImageCostCalculator.ForJob(metadata, 0.036042m);
        Assert.Equal("reservation", result.Basis); Assert.Equal(0.036042m, result.Usd);
    }
    [Fact]
    public void UnknownModelsAndMismatchedProfilesNeverInheritSunburstPricing()
    {
        using var doc = JsonDocument.Parse(Sample);
        Assert.Null(OpenAIImageCostCalculator.Calculate("future-unknown-model", doc.RootElement));
        Assert.Null(OpenAIImageCostCalculator.Reservation(new ImageAiOptions { Model = Model, Quality = "high" }));
        Assert.Null(OpenAIImageCostCalculator.Reservation(new ImageAiOptions { Model = Model, Size = "1536x1024" }));
        var metadata = JsonSerializer.Serialize(new { model = Model, responseModel = "unknown-model", usage = doc.RootElement });
        Assert.Equal("reservation", OpenAIImageCostCalculator.ForJob(metadata, 0.036042m).Basis);
    }
    [Fact]
    public void PreflightReservationUsesDocumentedObservedTokensAndSafetyFactor()
    {
        var options = new ImageAiOptions { Model = Model };
        Assert.Equal(0.036042m, OpenAIImageCostCalculator.Reservation(options));
        options.ReservationMultiplier = 3;
        Assert.Equal(0.054063m, OpenAIImageCostCalculator.Reservation(options));
        options.ReservationMultiplier = 0;
        Assert.Null(OpenAIImageCostCalculator.Reservation(options));
    }
    [Fact]
    public void DirectEditsDoNotApplyResponsesCachedInputDiscounts()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Sample)!; node["input_tokens_details"]!["cached_tokens"] = 1554;
        using var doc = JsonDocument.Parse(node.ToJsonString());
        Assert.Equal(0.018021m, OpenAIImageCostCalculator.Calculate(Model, doc.RootElement)!.TotalUsd);
    }
    [Fact]
    public void LegacyStringAndNewObjectUsageReconcileWithoutRewritingRecords()
    {
        using var doc = JsonDocument.Parse(Sample);
        foreach (var metadata in new[] { JsonSerializer.Serialize(new { model = Model, usage = Sample }), JsonSerializer.Serialize(new { model = Model, usage = doc.RootElement }) })
        {
            var result = OpenAIImageCostCalculator.ForJob(metadata, 0.10m);
            Assert.Equal("usage", result.Basis); Assert.Equal(0.018021m, result.Usd);
            Assert.Equal(0, OpenAIImageCostCalculator.ForJob(metadata, 0.10m, 123).Usd);
        }
        Assert.Equal(0.10m, OpenAIImageCostCalculator.ForJob("bad json", 0.10m).Usd);
    }
    [Fact]
    public async Task BudgetSeparatesUsageCostsFromUnknownReservationsAndRepricesHistoricalFlatValues()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(dbOptions);
        var metadata = JsonSerializer.Serialize(new { model = Model, usage = Sample });
        foreach (var value in new[] { metadata, metadata, null })
            db.ImageProcessingJobs.Add(new ImageProcessingJob { PipelineVersion = "openai-edit-v1:old-settings", StartedAt = DateTime.UtcNow, EstimatedCostUsd = 0.10m, SafetyReportJson = value, Status = ImageJobStatus.Failed });
        await db.SaveChangesAsync();
        var budget = await new ImageProcessingQueue(db, Options.Create(new ImageAiOptions { Enabled = true, Model = Model })).GetBudgetAsync(default);
        Assert.Equal(0.036042m, budget.UsageCostUsd); Assert.Equal(0.10m, budget.ReservedCostUsd);
        Assert.Equal(0.136042m, budget.SpentUsd); Assert.Equal(2, budget.UsagePricedRequests); Assert.Equal(1, budget.UnreconciledRequests);
        Assert.All(await db.ImageProcessingJobs.ToListAsync(), job => Assert.Equal(0.10m, job.EstimatedCostUsd));
    }
}

