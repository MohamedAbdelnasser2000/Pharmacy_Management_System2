using System;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public string? UserId { get; set; } // Null for global notifications
        
        public string Type { get; set; } // Info, Warning, Success, Error
        
        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = AppClock.UtcNow;

        public string? Link { get; set; } // Action link if applicable
    }
}
