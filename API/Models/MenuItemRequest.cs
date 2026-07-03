using System.ComponentModel.DataAnnotations;

namespace WebapplicationFactoryDemo.Api.Models;

public class MenuItemRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Calories { get; set; }

    [Range(0, double.MaxValue)]
    public double Price { get; set; }

    [Range(0, int.MaxValue)]
    public int ProteinGrams { get; set; }

    public bool IsBreakfast { get; set; }
}
