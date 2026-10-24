namespace Canteen.Model.DTOs
{
    public class CanteenTableDto
    {
        public int TableId { get; set; }
        public int CanteenId { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string QRToken { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class CreateCanteenTableDto
    {
        public int CanteenId { get; set; }
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
    }

    public class UpdateCanteenTableDto
    {
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
    }
}
