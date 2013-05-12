namespace UK_Crime_App.DTOs
{
    public class Crime
    {
        public string Id { get; set; }
        public string Context { get; set; }
        public string Category { get; set; }
        public string Month { get; set; }
        public string Location_Type { get; set; }
        public string Location_Subtype { get; set; }
        public Location Location { get; set; }
        public OutcomeStatus Outcome_Status { get; set; }
    }
}
