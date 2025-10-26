public class Cell
{
    public string Address { get; init; } = "";
    public string Expression { get; set; } = "";
    public string DisplayValue { get; set; } = "";
    public bool HasError { get; set; } = false;
}