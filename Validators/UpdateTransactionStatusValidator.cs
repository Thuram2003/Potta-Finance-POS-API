using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validatorsc
{
    // Validates transaction status updates
    public class UpdateTransactionStatusValidator : AbstractValidator<UpdateTransactionStatusDto>
    {
        private static readonly string[] ValidStatuses = { "Pending", "Completed", "Cancelled" };

        public UpdateTransactionStatusValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty()
                .WithMessage("Status is required")
                .Must(status => ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}");
        }
    }
}
