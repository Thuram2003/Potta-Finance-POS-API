using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates seat status updates
    public class UpdateSeatStatusValidator : AbstractValidator<UpdateSeatStatusDTO>
    {
        private static readonly string[] ValidStatuses = new[]
        {
            "Available",
            "Occupied",
            "Reserved",
            "Not Available"
        };

        public UpdateSeatStatusValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .WithMessage("Status is required")
                .Must(status => ValidStatuses.Contains(status))
                .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}");

            RuleFor(x => x.CustomerId)
                .NotEmpty()
                .When(x => x.Status == "Occupied" || x.Status == "Reserved")
                .WithMessage("CustomerId is required when status is Occupied or Reserved")
                .MaximumLength(50)
                .WithMessage("CustomerId cannot exceed 50 characters");
        }
    }
}
