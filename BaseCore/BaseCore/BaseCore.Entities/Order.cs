
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public class Order
    {
       
        public int Id { get; set; }

       
        public string UserId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } // Pending, Completed, Cancelled

        public string ShippingAddress { get; set; }

        public List<OrderDetail> OrderDetails { get; set; }
    }
}
