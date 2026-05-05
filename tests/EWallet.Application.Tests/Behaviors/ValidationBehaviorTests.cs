using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;
using EWallet.Application.Behaviors;

namespace EWallet.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    // ─── Dummy request / response types ──────────────────────────────────────

    private record TestRequest : IRequest<TestResponse>;
    private record TestResponse;

    // ─── Validation fails ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidationFails_ThrowsValidationExceptionBeforeCallingNext()
    {
        // Arrange – a validator that always reports one error
        var failingValidator = new InlineValidator<TestRequest>();
        failingValidator.RuleFor(_ => _).Must(_ => false).WithMessage("Always fails");

        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            new[] { (IValidator<TestRequest>)failingValidator });

        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResponse());
        };

        // Act
        Func<Task> act = () => behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert
        await act.Should()
                 .ThrowAsync<ValidationException>("a failed validation must short-circuit the pipeline");

        nextCalled.Should().BeFalse("next() must never be invoked when validation fails");
    }

    // ─── No validators registered ────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoValidatorsRegistered_CallsNextDelegate()
    {
        // Arrange – empty validator list simulates no validators in DI container
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            Enumerable.Empty<IValidator<TestRequest>>());

        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResponse());
        };

        // Act
        await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue("no validators means the request passes straight through");
    }

    // ─── Validation passes ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidationPasses_CallsNextAndReturnsResult()
    {
        // Arrange – a validator that always succeeds
        var passingValidator = new InlineValidator<TestRequest>(); // no rules = always valid

        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            new[] { (IValidator<TestRequest>)passingValidator });

        var expected = new TestResponse();
        RequestHandlerDelegate<TestResponse> next = () => Task.FromResult(expected);

        // Act
        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }
}
