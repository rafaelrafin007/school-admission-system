namespace SchoolAdmission.Application.Classes.DTOs;

public sealed record ClassResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);
