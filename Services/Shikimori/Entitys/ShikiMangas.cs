namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiMangas
{
    public long id { get; set; }
    public required string name { get; set; }
    public required string russian { get; set; }
    public required ShikiImage image { get; set; }
    public required string url { get; set; }
    public required string kind { get; set; }
    public required string score { get; set; }
    public required string status { get; set; }
    public long volumes { get; set; }
    public long chapters { get; set; }
    public required object aired_on { get; set; }
    public required object released_on { get; set; }
    public required List<object> english { get; set; }
    public required List<object> japanese { get; set; }
    public required List<object> synonyms { get; set; }
    public required object license_name_ru { get; set; }
    public required object description { get; set; }
    public required string description_html { get; set; }
    public required object description_source { get; set; }
    public required object franchise { get; set; }
    public bool favoured { get; set; }
    public bool anons { get; set; }
    public bool ongoing { get; set; }
    public long thread_id { get; set; }
    public long topic_id { get; set; }
    public long myanimelist_id { get; set; }
    public required List<object> rates_scores_stats { get; set; }
    public required List<object> rates_statuses_stats { get; set; }
    public required List<object> licensors { get; set; }
    public required List<object> genres { get; set; }
    public required List<object> publishers { get; set; }
    public required object user_rate { get; set; }
}
