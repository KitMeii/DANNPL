using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace BaseCore.Entities
{
    public class Category
    {
    
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }
    }
}
