namespace MyFirstMVCApp.ViewModels
{
    public class DashboardAnalyticsViewModel
    {
        public int TotalEmployees { get; set; }

        public int TotalDepartments { get; set; }

        public int TotalDesignations { get; set; }

        public List<DepartmentEmployeeCount> DepartmentCounts
        {
            get;
            set;
        } = new();

        public List<RecentEmployeeViewModel> RecentEmployees
        {
            get;
            set;
        } = new();
    }

    public class DepartmentEmployeeCount
    {
        public string Department { get; set; } = string.Empty;

        public int EmployeeCount { get; set; }
    }

    public class RecentEmployeeViewModel
    {
        public int Id { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Designation { get; set; } = string.Empty;
    }
}