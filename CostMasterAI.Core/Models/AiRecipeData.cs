using System.Text.Json.Serialization;

namespace CostMasterAI.Core.Models
{
    // Class ini cuma buat nampung respon JSON dari AI
    public class AiRecipeData
    {
        [JsonPropertyName("ingredient_name")]
        public string IngredientName { get; set; } = string.Empty;

        [JsonPropertyName("usage_qty")]
        public double UsageQty { get; set; }

        [JsonPropertyName("usage_unit")]
        public string UsageUnit { get; set; } = string.Empty;

        [JsonPropertyName("estimated_price_per_package")]
        public decimal EstimatedPrice { get; set; }

        [JsonPropertyName("package_qty")]
        public double PackageQty { get; set; }

        [JsonPropertyName("package_unit")]
        public string PackageUnit { get; set; } = string.Empty;
    }
}
