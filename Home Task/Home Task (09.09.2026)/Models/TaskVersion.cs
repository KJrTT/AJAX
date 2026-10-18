public class TaskVersion
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public int VersionNumber { get; set; }   
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public DateTime? PlannedStart { get; set; }  
    public DateTime? PlannedEnd { get; set; }     
}