using System.ComponentModel.DataAnnotations;

namespace BaseCore.Common
{
    public class Enums
    {
        //Not being used yet
        public enum ActiveStatus
        {
            [Display(Name = "INACTIVE")]
            InActive = 0,

            [Display(Name = "ACTIVE")]
            Active = 1
        }

        public enum TaskTemplateNameStatus
        {
            [Display(Name = "DRAFT")]
            InActive = 0,

            [Display(Name = "ACTIVE")]
            Active = 1
        }

        public enum ColorStatus
        {
            [Display(Name = "Development")]
            Development = 0,

            [Display(Name = "Finish")]
            Finish = 1
        }

        public enum ColorType
        {
            [Display(Name = "One")]
            One = 1,

            [Display(Name = "Two")]
            Two = 2,

            [Display(Name = "Three")]
            Three = 3
        }

        public enum DataStatus
        {
            [Display(Name = "Development")]
            Development = 0,

            [Display(Name = "Exist")]
            Exist = 1
        }

        public enum SizeRangeType
        {
            [Display(Name = "Standard")]
            Standard = 1,

            [Display(Name = "Special Order")]
            SpecialOrder = 2
        }

        public enum SizeRangePart
        {
            [Display(Name = "Upper Part")]
            UpperPart = 1,

            [Display(Name = "Lower Part")]
            LowerPart = 2
        }

        public enum SizeRangeGender
        {
            [Display(Name = "Men")]
            Men = 1,

            [Display(Name = "Women")]
            Women = 2,

            [Display(Name = "Kid")]
            Kid = 3,
        }

        public enum TaskSession
        {
            [Display(Name = "Spring")]
            Spring = 0,

            [Display(Name = "Summer")]
            Summer = 1,

            [Display(Name = "Autumn")]
            Autumn = 2,

            [Display(Name = "Winter")]
            Winter = 3
        }

        public enum TaskTemplateStatus
        {
            [Display(Name = "STATUS_INACTIVE")]
            InActive = 0,

            [Display(Name = "STATUS_ACTIVE")]
            Active = 1
        }

        public enum TaskHistoryStatus
        {
            [Display(Name = "Draft")]
            InActive = 0,

            [Display(Name = "Active")]
            Active = 1
        }

        public enum StatusActivity
        {
            All = -1,
            InActive = 0,
            Active = 1
        }

        public enum UserType
        {
            SystemAdmin = 1,
            Doctor = 2
        }

        public enum RoleType
        {
            SystemRole = 1,
            AgencyRole = 2
        }

        public enum DayType
        {
            Monday = 1,
            Tuesday = 2,
            Wenesday = 3,
            Thursday = 4,
            Friday = 5,
            Saturday = 6,
            Sunday = 0
        }

        public enum PriceType
        {
            Day = 1,
            Night = 2,
            Hour = 3,
            Week = 4,
            Month = 5,
            Adults = 6,
            Children = 7
        }

        public enum WareHouseType
        {
            Import = 1,
            Export = 2,
            Move = 3
        }

        public enum RoomStatusType
        {
            CheckIn = 1,
            OverDue = 2,
            Booked = 3,
            CheckOut = 4,
            NotArrive = 5,
            Repair = 6,
            Dirty = 7,
            Available = 8,
            Abort = 9
        }
        public enum ReservePriceType
        {
            ManualPrice = 1,
            FreePrice = 2,
        }


        public enum MediaType
        {
            [Display(Name ="Không xác định")]
            Unkown = 0,
            [Display(Name = "Hình ảnh")]
            Image = 1,
            [Display(Name = "Video")]
            Video = 2,
            [Display(Name = "Doc")]
            Doc = 3,
            [Display(Name = "Pdf")]
            Pdf = 4,
            [Display(Name = "File")]
            File = 5,
        }
    }
}
