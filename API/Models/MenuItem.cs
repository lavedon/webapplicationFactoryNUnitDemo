namespace WebapplicationFactoryDemo.Api.Models;

public class MenuItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Calories { get; set; }
    public double Price { get; set; }
    public int ProteinGrams { get; set; }
    public bool IsBreakfast { get; set; }
}
