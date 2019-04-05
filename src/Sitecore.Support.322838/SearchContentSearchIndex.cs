using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Client.Pipelines.Search;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Exceptions;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Pipelines.Search;
using Sitecore.Search;
using Sitecore.Shell;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.Support.ContentSearch.Client.Pipelines.Search
{
  public class SearchContentSearchIndex: Sitecore.ContentSearch.Client.Pipelines.Search.SearchContentSearchIndex
  {
    private ISettings settings;
    public SearchContentSearchIndex()
    {

    }
        
    public override void Process(SearchArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      if (!args.UseLegacySearchEngine)
      {
        if (!ContentSearchManager.Locator.GetInstance<IContentSearchConfigurationSettings>().ContentSearchEnabled())
        {
          args.UseLegacySearchEngine = true;
        }
        else if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsOn)
        {
          args.IsIndexProviderOn = false;
        }
        else
        {
          Item item = args.Root ?? args.Database.GetRootItem();
          Assert.IsNotNull(item, "rootItem");
          if (!args.TextQuery.IsNullOrEmpty())
          {
            ISearchIndex index;
            try
            {
              index = ContentSearchManager.GetIndex(new SitecoreIndexableItem(item));
            }
            catch (IndexNotFoundException)
            {
              SearchLog.Log.Warn("No index found for " + item.ID, null);
              return;
            }
            if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsIndexOn(index.Name))
            {
              args.IsIndexProviderOn = false;
            }
            else
            {
              if (settings == null)
              {
                settings = index.Locator.GetInstance<ISettings>();
              }
              using (IProviderSearchContext providerSearchContext = index.CreateSearchContext(SearchSecurityOptions.Default))
              {
                List<SitecoreUISearchResultItem> results = new List<SitecoreUISearchResultItem>();
                try
                {
                  IQueryable<SitecoreUISearchResultItem> queryable = null;
                  if (args.Type != SearchType.ContentEditor)
                  {
                    queryable = new GenericSearchIndex().Search(args, providerSearchContext);
                  }
                  if (queryable == null || Enumerable.Count(queryable) == 0)
                  {
                    queryable = ((!(args.ContentLanguage != (Language)null) || args.ContentLanguage.Name.IsNullOrEmpty()) ? (from i in providerSearchContext.GetQueryable<SitecoreUISearchResultItem>()
                                                                                                                             where i.Name.StartsWith(args.TextQuery) || i.Content.Contains(args.TextQuery)
                                                                                                                             select i) : providerSearchContext.GetQueryable<SitecoreUISearchResultItem>().Where((SitecoreUISearchResultItem i) => i.Name.StartsWith(args.TextQuery) || (i.Content.Contains(args.TextQuery) && i.Language.Equals(args.ContentLanguage.Name))));
                  }
                  if (args.Root != null && args.Type != SearchType.ContentEditor)
                  {
                    queryable = from i in queryable
                                where i.Paths.Contains(args.Root.ID)
                                select i;
                  }
                  foreach (SitecoreUISearchResultItem item2 in Enumerable.TakeWhile(queryable, (SitecoreUISearchResultItem result) => results.Count < args.Limit))
                  {
                    if (!UserOptions.View.ShowHiddenItems)
                    {
                      Item sitecoreItem = GetSitecoreItem(item2);
                      if (sitecoreItem != null && IsHidden(sitecoreItem))
                      {
                        continue;
                      }
                    }
                    SitecoreUISearchResultItem sitecoreUISearchResultItem = results.FirstOrDefault((SitecoreUISearchResultItem r) => r.ItemId == item2.ItemId);
                    if (sitecoreUISearchResultItem == null)
                    {
                      results.Add(item2);
                    }
                    else if (args.ContentLanguage != (Language)null && !args.ContentLanguage.Name.IsNullOrEmpty())
                    {
                      if ((sitecoreUISearchResultItem.Language != args.ContentLanguage.Name && item2.Language == args.ContentLanguage.Name) || (sitecoreUISearchResultItem.Language == item2.Language && sitecoreUISearchResultItem.Uri.Version.Number < item2.Uri.Version.Number))
                      {
                        results.Remove(sitecoreUISearchResultItem);
                        results.Add(item2);
                      }
                    }
                    else if (args.Type != SearchType.Classic)
                    {
                      if (sitecoreUISearchResultItem.Language == item2.Language && sitecoreUISearchResultItem.Uri.Version.Number < item2.Uri.Version.Number)
                      {
                        results.Remove(sitecoreUISearchResultItem);
                        results.Add(item2);
                      }
                    }
                    else
                    {
                      results.Add(item2);
                    }
                  }
                }
                catch (Exception exception)
                {
                  Log.Error("Invalid lucene search query: " + args.TextQuery, exception, this);
                  return;
                }
                FillSearchResult(results, args);
              }
            }
          }
        }
      }
    }

    private bool IsHidden(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      if (!item.Appearance.Hidden)
      {
        if (item.Parent != null)
        {
          return IsHidden(item.Parent);
        }
        return false;
      }
      return true;
    }
  }
}