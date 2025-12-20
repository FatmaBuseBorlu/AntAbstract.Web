using System;
using System.ComponentModel.DataAnnotations;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{

    public abstract class BaseEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    }
}