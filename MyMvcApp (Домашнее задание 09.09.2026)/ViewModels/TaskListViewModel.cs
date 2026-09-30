namespace MyMvcApp.ViewModels;

public class TaskListViewModel
{
    public string Title { get; set; } = "Планировщик задач";
    public string ApiEndpoint { get; set; } = "/api/tasks";
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
}