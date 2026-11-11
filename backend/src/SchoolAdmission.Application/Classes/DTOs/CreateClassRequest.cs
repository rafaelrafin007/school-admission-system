using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Application.Classes.DTOs;

public sealed record CreateClassRequest
{
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(160, ErrorMessage = "Class name must not exceed 160 characters.")]
    public string? Name { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Total seats must be a positive integer.")]
    public int TotalSeats { get; init; }
}
