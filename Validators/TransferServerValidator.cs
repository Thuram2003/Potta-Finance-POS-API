using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for TransferServerRequest
    /// </summary>
    public class TransferServerValidator : AbstractValidator<TransferServerRequest>
    {
        public TransferServerValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty().WithMessage("Transaction ID is required")
                .MaximumLength(50).WithMessage("Transaction ID cannot exceed 50 characters");

            RuleFor(x => x.NewStaffId)
                .GreaterThan(0).WithMessage("New staff ID must be greater than 0");

            RuleFor(x => x.Reason)
                .MaximumLength(200).WithMessage("Reason cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Reason));
        }
    }
}
