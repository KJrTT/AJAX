namespace Домашнее_задание__07._09._2026_.Models
{
    public class ApplicationStorage
    {
        public static List<ApplicationForm> Items { get; } = new();
        public static int NextId = 1;
    }
}
