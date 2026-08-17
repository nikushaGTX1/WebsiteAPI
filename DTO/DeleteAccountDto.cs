using System.ComponentModel.DataAnnotations;

namespace Website_API.DTO;

public sealed class DeleteAccountDto
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
