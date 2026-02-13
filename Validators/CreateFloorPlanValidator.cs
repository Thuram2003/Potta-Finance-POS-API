using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates floor plan creation and updates
    public class CreateFloorPlanValidator : AbstractValidator<FloorPlanDetailDto>
    {
        public CreateFloorPlanValidator()
        {
            RuleFor(x => x.FloorName)
                .NotEmpty()
                .WithMessage("Floor name is required")
                .MaximumLength(100)
                .WithMessage("Floor name cannot exceed 100 characters");

            RuleFor(x => x.FloorNumber)
                .GreaterThan(0)
                .WithMessage("Floor number must be greater than 0")
                .LessThanOrEqualTo(100)
                .WithMessage("Floor number cannot exceed 100");

            RuleFor(x => x.CanvasWidth)
                .GreaterThan(0)
                .WithMessage("Canvas width must be greater than 0")
                .LessThanOrEqualTo(10000)
                .WithMessage("Canvas width cannot exceed 10000");

            RuleFor(x => x.CanvasHeight)
                .GreaterThan(0)
                .WithMessage("Canvas height must be greater than 0")
                .LessThanOrEqualTo(10000)
                .WithMessage("Canvas height cannot exceed 10000");

            RuleFor(x => x.GridSpacing)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Grid spacing cannot be negative")
                .LessThanOrEqualTo(100)
                .WithMessage("Grid spacing cannot exceed 100");

            // Validate elements if provided
            RuleForEach(x => x.Elements).ChildRules(element =>
            {
                element.RuleFor(x => x.ElementType)
                    .NotEmpty()
                    .WithMessage("Element type is required");

                element.RuleFor(x => x.XPosition)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("X position cannot be negative");

                element.RuleFor(x => x.YPosition)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Y position cannot be negative");

                element.RuleFor(x => x.Width)
                    .GreaterThan(0)
                    .WithMessage("Width must be greater than 0");

                element.RuleFor(x => x.Height)
                    .GreaterThan(0)
                    .WithMessage("Height must be greater than 0");

                element.RuleFor(x => x.Rotation)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Rotation cannot be negative")
                    .LessThanOrEqualTo(360)
                    .WithMessage("Rotation cannot exceed 360 degrees");

                element.RuleFor(x => x.ZIndex)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Z-index cannot be negative");
            });
        }
    }
}
