using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for mobile complete payment requests
    /// </summary>
    public class MobileCompletePaymentValidator : AbstractValidator<MobileCompletePaymentRequest>
    {
        public MobileCompletePaymentValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty()
                .WithMessage("Transaction ID is required")
                .MaximumLength(50)
                .WithMessage("Transaction ID cannot exceed 50 characters");

            RuleFor(x => x.StaffId)
                .GreaterThan(0)
                .WithMessage("Staff ID must be greater than 0");

            RuleFor(x => x.PaymentMethod)
                .NotEmpty()
                .WithMessage("Payment method is required")
                .Must(BeValidPaymentMethod)
                .WithMessage("Payment method must be 'Cash', 'MTN Mobile Money', or 'Orange Money'");

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Amount must be greater than 0")
                .LessThanOrEqualTo(10000000)
                .WithMessage("Amount cannot exceed 10,000,000");

            RuleFor(x => x.Reference)
                .MaximumLength(200)
                .WithMessage("Reference cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Reference));
        }

        private bool BeValidPaymentMethod(string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return false;

            var validMethods = new[] { "Cash", "MTN Mobile Money", "Orange Money" };
            return validMethods.Contains(paymentMethod);
        }
    }
}
