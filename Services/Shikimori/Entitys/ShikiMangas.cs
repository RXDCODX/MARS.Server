#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MARS.Server.Services.Shikimori.Entitys;

public class ShikiMangas
{
    public long id { get; set; }
    public string name { get; set; }
    public string russian { get; set; }
    public ShikiImage image { get; set; }
    public string url { get; set; }
    public string kind { get; set; }
    public string score { get; set; }
    public string status { get; set; }
    public long volumes { get; set; }
    public long chapters { get; set; }
    public object aired_on { get; set; }
    public object released_on { get; set; }
    public List<object> english { get; set; }
    public List<object> japanese { get; set; }
    public List<object> synonyms { get; set; }
    public object license_name_ru { get; set; }
    public object description { get; set; }
    public string description_html { get; set; }
    public object description_source { get; set; }
    public object franchise { get; set; }
    public bool favoured { get; set; }
    public bool anons { get; set; }
    public bool ongoing { get; set; }
    public long thread_id { get; set; }
    public long topic_id { get; set; }
    public long myanimelist_id { get; set; }
    public List<object> rates_scores_stats { get; set; }
    public List<object> rates_statuses_stats { get; set; }
    public List<object> licensors { get; set; }
    public List<object> genres { get; set; }
    public List<object> publishers { get; set; }
    public object user_rate { get; set; }
}
