namespace Website_API.Services;

/// <summary>
/// A problem with what the user sent (too many photos, not an image, ...). The global error
/// handler turns it into a 400 with this message; every other exception becomes a plain 500
/// without internal details.
/// </summary>
public sealed class UserInputException(string message) : InvalidOperationException(message);
