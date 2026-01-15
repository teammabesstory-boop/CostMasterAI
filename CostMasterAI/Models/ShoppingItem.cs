namespace CostMasterAI.Models
{
    public class ShoppingItem
    {
        public string IngredientName { get; set; } = string.Empty;
        public double TotalQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public string Category { get; set; } = "General";
    }
}