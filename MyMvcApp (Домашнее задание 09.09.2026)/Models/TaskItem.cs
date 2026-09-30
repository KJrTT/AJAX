public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime DueDate { get; set; }
    public string Priority { get; set; } = "Normal"; 
    public string Status { get; set; } = "New";      
    public string Category { get; set; } = "";
}