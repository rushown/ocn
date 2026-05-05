using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using EWallet.Application.Behaviors;

namespace EWallet.Application.Tests.Behaviors;

public class LoggingBehaviorTests
{
    // ─── Dummy request / response types ──────────────────────────────────────

    private record TestRequest : IRequest<TestResponse>;
    private record TestResponse;

    // ─── Logger receives at least one call per request ───────────────────────

    [Fact]
    public async Task Handle_AnyRequest_LogsAtLeastOnce()
    {
        // Arrange
        var logger = new Mock<ILogger<LoggingBehavior<TestRequest, TestResponse>>>();

        var behavior = new LoggingBehavior<TestRequest, TestResponse>(logger.Object);

        RequestHandlerDelegate<TestResponse> next =
            () => Task.FromResult(new TestResponse());

        // Act
        await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert – any log call is acceptable (entry, exit, or both)
        logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v != null),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            because: "the logging behavior must emit at least one log entry per handled request");
    }

    // ─── Next delegate is always called ──────────────────────────────────────

    [Fact]
    public async Task Handle_Always_CallsNextDelegate()
    {
        // Arrange
        var logger = new Mock<ILogger<LoggingBehavior<TestRequest, TestResponse>>>();
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(logger.Object);

        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResponse());
        };

        // Act
        await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue("LoggingBehavior must never swallow the pipeline");
    }

    // ─── Exception is re-thrown after logging ────────────────────────────────

    [Fact]
    public async Task Handle_NextThrows_ExceptionBubblesUpAfterLogging()
    {
        // Arrange
        var logger = new Mock<ILogger<LoggingBehavior<TestRequest, TestResponse>>>();
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(logger.Object);

        RequestHandlerDelegate<TestResponse> next =
            () => throw new InvalidOperationException("downstream failure");

        // Act
        Func<Task> act = () => behavior.Handle(new TestRequest(), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        // And still logs (error-level logging on exception is expected)
        logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v != null),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
