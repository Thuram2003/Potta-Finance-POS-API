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

            // CustomerId is optional — mobile staff select seats before a customer
            // or order is assigned, so it won't always be available.
            RuleFor(x => x.CustomerId)
                .MaximumLength(50)
                .WithMessage("CustomerId cannot exceed 50 characters")
                .When(x => x.CustomerId != null);
        }
    }
}
