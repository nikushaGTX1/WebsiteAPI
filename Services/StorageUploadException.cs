namespace Website_API.Services;

public sealed class StorageUploadException(bool transient, int? statusCode = null, Exception? inner = null)
    : InvalidOperationException("Photo upload failed.", inner)
{
    public bool IsTransient { get; } = transient;
    public int? StatusCode { get; } = statusCode;
}
