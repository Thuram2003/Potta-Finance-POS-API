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

            RuleFor(x => x.CustomerId)
                .NotEmpty()
                .When(x => x.Status == "Occupied" || x.Status == "Reserved")
                .WithMessage("CustomerId is required when status is Occupied or Reserved")
                .MaximumLength(50)
                .WithMessage("CustomerId cannot exceed 50 characters");

            RuleFor(x => x.TransactionId)
                .NotEmpty()
                .When(x => x.Status == "Occupied")
                .WithMessage("TransactionId is required when status is Occupied")
                .MaximumLength(50)
                .WithMessage("TransactionId cannot exceed 50 characters");
        }
    }
}
