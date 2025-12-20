using System.ComponentModel.DataAnnotations;
using AntAbstract.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;


namespace AntAbstract.Domain.Entities
{
    public class AccommodationBooking : BaseEntity
    {

        public string AppUserId { get; set; }
        public AppUser AppUser { get; set; }


        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; }


        public Guid RoomTypeId { get; set; }
        public RoomType RoomType { get; set; }


        public Guid? TransferOptionId { get; set; }
        public TransferOption? TransferOption { get; set; }


        public DateTime CheckInDate { get; set; } 
        public DateTime CheckOutDate { get; set; } 

        public string? RoommateName { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } 

        public bool IsPaid { get; set; } = false; 
    }
}