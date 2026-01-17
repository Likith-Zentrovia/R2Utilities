using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml;
using R2Library.Data.ADO.R2.DataServices;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Utilities;
using R2V2.Core.MyR2;
using R2V2.Core.Resource;
using R2V2.Core.Resource.PracticeArea;
using R2V2.Core.Search;

namespace R2Utilities.Tasks.DataConversion;

public class ConvertUserSearchDataTask : TaskBase
{
	private readonly DisciplineToSpecialtyDataService _disciplineToSpecialtyDataService;

	private readonly IQueryable<PracticeArea> _practiceAreas;

	private readonly IQueryable<Resource> _resources;

	private readonly IQueryable<UserSavedSearch> _userSavedSearches;

	private readonly IQueryable<UserSearchHistory> _userSearchHistories;

	public ConvertUserSearchDataTask(IQueryable<UserSearchHistory> userSearchHistories, IQueryable<UserSavedSearch> userSavedSearches, IQueryable<Resource> resources, IQueryable<PracticeArea> practiceAreas)
		: base("ConvertUserSearchDataTask", "-ConvertUserSearchData", "x99", TaskGroup.Deprecated, "Converts user search data, only needed during conversion to R2v2", enabled: false)
	{
		_userSearchHistories = userSearchHistories;
		_userSavedSearches = userSavedSearches;
		_resources = resources;
		_practiceAreas = practiceAreas;
		_disciplineToSpecialtyDataService = new DisciplineToSpecialtyDataService();
	}

	public override void Run()
	{
		try
		{
			TaskResultStep step2 = new TaskResultStep
			{
				Name = "SearchHistoryTask",
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step2);
			UpdateTaskResult();
			int count = 0;
			IList<UserSavedSearch> userSavedSearches = GetUserSavedSearch();
			foreach (UserSavedSearch userSavedSearch in userSavedSearches)
			{
				count++;
				R2UtilitiesBase.Log.InfoFormat("{0} of {1}", count, userSavedSearches.Count());
				R2UtilitiesBase.Log.DebugFormat("Id: {0}, CreationDate: {1}, Xml: {2}", userSavedSearch.Id, userSavedSearch.CreationDate, userSavedSearch.Xml);
				SearchSummary searchSummary = GetSearchSummary(userSavedSearch.Xml);
				if (searchSummary != null)
				{
					JavaScriptSerializer scriptSerializer = new JavaScriptSerializer();
					string json = scriptSerializer.Serialize(searchSummary);
					R2UtilitiesBase.Log.Debug(json);
					userSavedSearch.SearchQuery = json;
					userSavedSearch.ResultsCount = searchSummary.ResultsCount;
				}
			}
			UserSavedSearchService userSavedSearchService = new UserSavedSearchService();
			foreach (UserSavedSearch userSavedSearch2 in userSavedSearches)
			{
				userSavedSearchService.UpdateSearchQuery(userSavedSearch2.Id, userSavedSearch2.SearchQuery, userSavedSearch2.ResultsCount);
			}
			IList<UserSearchHistory> userSearchHistories = GetUserSearchHistory(new DateTime(2011, 1, 1, 0, 0, 0, 0), DateTime.Now);
			count = 0;
			foreach (UserSearchHistory userSearchHistory in userSearchHistories)
			{
				count++;
				R2UtilitiesBase.Log.InfoFormat("{0} of {1}", count, userSearchHistories.Count());
				R2UtilitiesBase.Log.DebugFormat("Id: {0}, CreationDate: {1}, SearchXml: {2}", userSearchHistory.Id, userSearchHistory.CreationDate, userSearchHistory.SearchXml);
				SearchSummary searchSummary2 = GetSearchSummary(userSearchHistory.SearchXml);
				if (searchSummary2 != null)
				{
					JavaScriptSerializer scriptSerializer2 = new JavaScriptSerializer();
					string json2 = scriptSerializer2.Serialize(searchSummary2);
					R2UtilitiesBase.Log.Debug(json2);
					userSearchHistory.SearchQuery = json2;
					userSearchHistory.ResultsCount = searchSummary2.ResultsCount;
				}
			}
			UserSearchHistoryService userSearchHistoryService = new UserSearchHistoryService();
			foreach (UserSearchHistory userSearchHistory2 in userSearchHistories)
			{
				userSearchHistoryService.UpdateSearchQuery(userSearchHistory2.Id, userSearchHistory2.SearchQuery, userSearchHistory2.ResultsCount);
			}
			UpdateTaskResult();
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private IList<UserSavedSearch> GetUserSavedSearch()
	{
		return (from x in _userSavedSearches
			where x.SearchQuery == null
			orderby x.CreationDate descending
			select x).ToList();
	}

	private IList<UserSearchHistory> GetUserSearchHistory(DateTime minDate, DateTime maxDate)
	{
		return (from x in _userSearchHistories
			where x.CreationDate >= minDate && x.CreationDate <= maxDate
			where x.SearchQuery == null
			orderby x.CreationDate descending
			select x).ToList();
	}

	private SearchSummary GetSearchSummary(string searchXml)
	{
		try
		{
			SearchSummary searchSummary = new SearchSummary();
			XmlDocument xmlDoc = new XmlDocument();
			string cleanXml = searchXml.Replace(" & ", " &amp; ");
			xmlDoc.LoadXml(cleanXml);
			XmlNode searchNode = XmlHelper.GetXmlNode(xmlDoc, "//searchroot/search");
			string searchType = searchNode.Attributes["type"].Value;
			searchSummary.Advanced = searchType == "2";
			XmlNode resultsNode = XmlHelper.GetXmlNode(xmlDoc, "//searchroot/searchresults");
			if (resultsNode != null)
			{
				string searchResults = resultsNode.InnerText;
				int.TryParse(searchResults, out var count);
				searchSummary.ResultsCount = count;
			}
			else
			{
				searchSummary.ResultsCount = 0;
			}
			foreach (XmlNode childNode in searchNode.ChildNodes)
			{
				switch (childNode.Name)
				{
				case "searchresources":
				{
					string resources = childNode.InnerText;
					break;
				}
				case "searchonly":
				{
					string searchOnly = childNode.InnerText;
					if (searchOnly == "3")
					{
						searchSummary.DrugMonograph = true;
						searchSummary.Field = SearchFields.All;
					}
					else if (searchOnly == "1")
					{
						searchSummary.Field = SearchFields.ImageTitle;
					}
					else
					{
						searchSummary.Field = SearchFields.All;
					}
					break;
				}
				case "searcharchive":
				{
					string archive = childNode.InnerText;
					searchSummary.Archive = archive == "1";
					searchSummary.Active = archive != "1";
					break;
				}
				case "searchcriteria":
					foreach (XmlNode criteriaNode in childNode.ChildNodes)
					{
						if (criteriaNode.Name == "criteria")
						{
							if (criteriaNode.Attributes != null)
							{
								string type = criteriaNode.Attributes["type"].Value.ToLower();
								string phrase = criteriaNode.Attributes["phrase"].Value.Trim();
								switch (type)
								{
								case "keyword":
									searchSummary.Term = phrase;
									break;
								case "isbn":
									searchSummary.Isbns = new string[1] { phrase };
									break;
								case "booktitle":
									searchSummary.BookTitle = phrase;
									break;
								case "author":
									searchSummary.Author = phrase;
									break;
								case "publisher":
									searchSummary.Publisher = phrase;
									break;
								case "editor":
									searchSummary.Editor = phrase;
									break;
								default:
									R2UtilitiesBase.Log.WarnFormat("criteria type not supported: {0}", type);
									break;
								}
								foreach (XmlAttribute attribute in criteriaNode.Attributes)
								{
									if (attribute.Name != "type" && attribute.Name != "phrase")
									{
										R2UtilitiesBase.Log.WarnFormat("criteria attribute not supported: {0}", attribute.Name);
									}
								}
							}
							else
							{
								R2UtilitiesBase.Log.Warn("criteria attribute is null");
							}
						}
						else
						{
							R2UtilitiesBase.Log.WarnFormat("searchcriteria node not supported: {0}", criteriaNode.Name);
						}
					}
					break;
				case "limitcriteria":
				{
					string limitCriteriaType = childNode.Attributes["type"].Value;
					foreach (XmlNode limitNode in childNode.ChildNodes)
					{
						switch (limitNode.Name)
						{
						case "limitdiscipline":
						{
							int disciplineId = int.Parse(limitNode.InnerText);
							if (disciplineId > 0)
							{
								R2UtilitiesBase.Log.WarnFormat("DISIPLINE ID: {0}", disciplineId);
								string specialtyCode = _disciplineToSpecialtyDataService.GetSpecialtyCodeByDisciplineId(disciplineId);
								if (specialtyCode == null)
								{
									R2UtilitiesBase.Log.Warn("Specialty code was null");
									return null;
								}
								searchSummary.SpecialtyCode = specialtyCode;
							}
							break;
						}
						case "limitresource":
						{
							int resourceId = int.Parse(limitNode.InnerText);
							if (resourceId > 0)
							{
								Resource resource = GetResourceById(resourceId);
								searchSummary.SearchWithinIsbns = new string[1] { resource.Isbn };
							}
							break;
						}
						case "limitlibrary":
						{
							int libraryId = int.Parse(limitNode.InnerText);
							if (libraryId > 0)
							{
								PracticeArea practiceArea = _practiceAreas.SingleOrDefault((PracticeArea x) => x.Id == libraryId);
								searchSummary.PracticeAreaCode = practiceArea.Code;
							}
							break;
						}
						case "limitreserveshelf":
						{
							int reserveShelfId = int.Parse(limitNode.InnerText);
							if (reserveShelfId > 0)
							{
								searchSummary.ReserveShelfId = reserveShelfId;
							}
							break;
						}
						default:
							R2UtilitiesBase.Log.WarnFormat("limit criteria node not supported: {0}", limitNode.Name);
							break;
						}
					}
					break;
				}
				default:
					R2UtilitiesBase.Log.WarnFormat("search node not supported: {0}", childNode.Name);
					break;
				}
			}
			return searchSummary;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Warn(ex.Message, ex);
			return null;
		}
	}

	private SearchData ParseSearchXml(UserSearchHistory userSearchHistory)
	{
		SearchData searchData = new SearchData
		{
			UserSearchHistoryId = userSearchHistory.Id,
			UserId = userSearchHistory.UserId,
			CreatedBy = userSearchHistory.CreatedBy,
			DateCreated = userSearchHistory.CreationDate,
			SearchXml = userSearchHistory.SearchXml
		};
		R2UtilitiesBase.Log.DebugFormat("UserSearchHistoryId: {0}, DateCreated: {1}, SearchXml: {2}", searchData.UserSearchHistoryId, searchData.DateCreated, searchData.SearchXml);
		XmlDocument xmlDoc = new XmlDocument();
		string cleanXml = userSearchHistory.SearchXml.Replace(" & ", " &amp; ");
		xmlDoc.LoadXml(cleanXml);
		XmlNode searchNode = XmlHelper.GetXmlNode(xmlDoc, "//searchroot/search");
		searchData.SearchType = searchNode.Attributes["type"].Value;
		XmlNode resultsNode = XmlHelper.GetXmlNode(xmlDoc, "//searchroot/searchresults");
		searchData.Results = resultsNode.InnerText;
		foreach (XmlNode childNode in searchNode.ChildNodes)
		{
			switch (childNode.Name)
			{
			case "searchresources":
				searchData.Resources = childNode.InnerText;
				break;
			case "searchonly":
				searchData.SearchOnly = childNode.InnerText;
				break;
			case "searcharchive":
				searchData.Archive = childNode.InnerText;
				break;
			case "searchcriteria":
				foreach (XmlNode criteriaNode in childNode.ChildNodes)
				{
					if (criteriaNode.Name == "criteria")
					{
						SearchCriteria searchCriteria = new SearchCriteria();
						searchData.SearchCriteria.Add(searchCriteria);
						if (criteriaNode.Attributes == null)
						{
							continue;
						}
						foreach (XmlAttribute attribute in criteriaNode.Attributes)
						{
							if (attribute.Name == "type")
							{
								searchCriteria.Type = attribute.Value;
								continue;
							}
							if (attribute.Name == "phrase")
							{
								searchCriteria.Phrase = attribute.Value.Trim();
								continue;
							}
							R2UtilitiesBase.Log.WarnFormat("criteria attribute not supported: {0}", attribute.Name);
						}
						if (!string.IsNullOrEmpty(childNode.InnerText))
						{
							R2UtilitiesBase.Log.WarnFormat("criteria inner text not supported: '{0}'", childNode.InnerText);
						}
					}
					else
					{
						R2UtilitiesBase.Log.WarnFormat("searchcriteria node not supported: {0}", criteriaNode.Name);
					}
				}
				break;
			case "limitcriteria":
				searchData.LimitCriteriaType = childNode.Attributes["type"].Value;
				foreach (XmlNode limitNode in childNode.ChildNodes)
				{
					LimitCriteria limitCriteria = new LimitCriteria();
					searchData.LimitCriteria.Add(limitCriteria);
					switch (limitNode.Name)
					{
					case "limitdiscipline":
						limitCriteria.Discipline = int.Parse(limitNode.InnerText);
						break;
					case "limitresource":
						limitCriteria.Resource = int.Parse(limitNode.InnerText);
						break;
					case "limitlibrary":
						limitCriteria.Library = int.Parse(limitNode.InnerText);
						break;
					case "limitreserveshelf":
						limitCriteria.ReserverShelf = int.Parse(limitNode.InnerText);
						break;
					default:
						R2UtilitiesBase.Log.WarnFormat("limit criteria node not supported: {0}", limitNode.Name);
						break;
					}
				}
				break;
			default:
				R2UtilitiesBase.Log.WarnFormat("search node not supported: {0}", childNode.Name);
				break;
			}
		}
		return searchData;
	}

	private Resource GetResourceById(int resourceId)
	{
		return _resources.SingleOrDefault((Resource x) => x.Id == resourceId);
	}
}
