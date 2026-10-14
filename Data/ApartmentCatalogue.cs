using Microsoft.EntityFrameworkCore;
using Website_API.Models;
namespace Website_API.Data;

public static class ApartmentCatalogue
{
    // Keep the SQL snapshot predicate and public LINQ filter aligned. Integration
    // tests cover both imported source types, approval and case-insensitive matching.
    public const string PublishedSqlPredicate = """
        "IsApproved" AND
        "Description" NOT ILIKE '%Source: https://www.myhome.ge/%' AND
        "Description" NOT ILIKE '%Source: https://home.ss.ge/%'
        """;
    public static IQueryable<Apartment> Published(IQueryable<Apartment> apartments) => apartments.Where(a =>
        a.IsApproved && !EF.Functions.ILike(a.Description, "%Source: https://www.myhome.ge/%") &&
        !EF.Functions.ILike(a.Description, "%Source: https://home.ss.ge/%"));
}
