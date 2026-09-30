namespace MyMvcApp.ViewModels;

public record TaskDto(int Id, string Title, string Description,
                     DateTime DueDate, string Priority, string Status, string Category);

public record TaskVersionDto(int Id, int VersionNumber, DateTime CreatedAt,
                            string Title, string Description,
                            DateTime? PlannedStart, DateTime? PlannedEnd);