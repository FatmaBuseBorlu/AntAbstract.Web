using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    public class SystemParameter
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Group { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public int Order { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}