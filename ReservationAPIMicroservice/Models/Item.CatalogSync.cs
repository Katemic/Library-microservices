namespace ReservationAPIMicroservice.Models
{

    public partial class Item
    {
        public long ItemVersion { get; set; }

        public DateTime? DeletedAtUtc { get; set; }
    }

}
