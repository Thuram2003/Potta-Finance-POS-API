using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates table status updates
    public class UpdateTableStatusValidator : AbstractValidator<UpdateTableStatusDTO>
    {
        private static readonly string[] ValidStatuses = new[]
        {
            "Available",
            "Occupied",
            "Reserved",
            "Not Available"
        };

        public UpdateTableStatusValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .WithMessage("Status is required")
                .Must(status => ValidStatuses.Contains(status))
                .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}");

            // CustomerId and TransactionId are optional — mobile staff select seats
            // before creating an order, so these values aren't available at that point.
            RuleFor(x => x.CustomerId)
                .MaximumLength(50)
                .WithMessage("CustomerId cannot exceed 50 characters")
                .When(x => x.CustomerId != null);

            RuleFor(x => x.TransactionId)
                .MaximumLength(50)
                .WithMessage("TransactionId cannot exceed 50 characters")
                .When(x => x.TransactionId != null);
        }
    }
}
