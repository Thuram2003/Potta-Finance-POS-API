using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates staff login request
    public class StaffLoginValidator : AbstractValidator<StaffLoginRequest>
    {
        public StaffLoginValidator()
        {
            RuleFor(x => x.DailyCode)
                .NotEmpty()
                .WithMessage("Daily code is required")
                .Length(4)
                .WithMessage("Daily code must be 4 digits")
                .Matches("^[0-9]+$")
                .WithMessage("Daily code must contain only numbers");
        }
    }
}
