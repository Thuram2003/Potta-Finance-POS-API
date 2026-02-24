using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for MoveOrderRequest
    /// </summary>
    public class MoveOrderValidator : AbstractValidator<MoveOrderRequest>
    {
        public MoveOrderValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty().WithMessage("Transaction ID is required")
                .MaximumLength(50).WithMessage("Transaction ID cannot exceed 50 characters");

            RuleFor(x => x.TargetTableId)
                .NotEmpty().WithMessage("Target table ID is required")
                .MaximumLength(50).WithMessage("Target table ID cannot exceed 50 characters");

            RuleFor(x => x.Reason)
                .MaximumLength(200).WithMessage("Reason cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Reason));
        }
    }
}
