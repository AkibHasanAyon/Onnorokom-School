namespace SchoolAdmission.Application.Abstractions.Messaging;

/// <summary>A request made on behalf of a student (X-Student-Id). Lets LoggingBehavior log the id masked.</summary>
public interface IStudentRequest
{
    string? StudentId { get; }
}
