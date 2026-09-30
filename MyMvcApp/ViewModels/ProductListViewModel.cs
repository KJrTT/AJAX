namespace MyMvcApp.ViewModels;

public class ProductListViewModel
{
    public string Title { get; set; } = "Каталог";
    public string ApiEndpoint { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
}