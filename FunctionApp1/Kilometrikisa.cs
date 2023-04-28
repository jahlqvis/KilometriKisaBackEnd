using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kilometrikisa
{
    public class Kilometrikisa
    {
        const string kkPageUrlStart = "https://www.kilometrikisa.fi";
        const string loginPageUrl = kkPageUrlStart + "/accounts/login/";
        const string myTeamsUrl = kkPageUrlStart + "/accounts/myteams/";
        const string profilePageUrl = kkPageUrlStart + "/accounts/profile/";
        const string contestID = "1";
        const string updateLogPageUrl = kkPageUrlStart + "/contest/log-save/";
        const bool axiosRequestWithAuth = true;
        const string loginErrorReply = "Antamasi tunnus tai salasana oli väärä";

        private HttpClientHandler handler;
        private HttpClient httpClient;

        public class User
        {
            public string Nickname { get; set; }
            public string Firstname { get; set; }
            public string Lastname { get; set; }
            public string Email { get; set; }
            public string Municipality { get; set; }
        }

        public class Contest
        {
            public string Name { get; set; }
            public string Link { get; set; }
        }

        public class SingleResult
        {
            public string Date { get; set; }
            public double Km { get; set; }
        }

        public class TeamInfo
        {
            public int Rank { get; set; }
            public string Name { get; set; }
            public double Kmpp { get; set; }
            public double KmTotal { get; set; }
            public double Days { get; set; }
        }

        public class TeamResult
        {
            public string TeamName { get; set; }
            public string Contest { get; set; }
            public string Time { get; set; }
            public string Link { get; set; }
            public string Year { get; set; }
            public string ContestId { get; set; }
        }

        public class Result
        {
            public int Rank { get; set; }
            public string Name { get; set; }
            public double Km { get; set; }
            public int Days { get; set; }
        }

        public class TeamStatus
        {
            public string Name { get; set; }
            public string Rank { get; set; }
            public List<Result> Results { get; set; }

        }

        public Kilometrikisa()
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AllowAutoRedirect = true
            };
            httpClient = new HttpClient(handler);
        }

        public string JsonDataUrlStart(string contestId)
        {
            return $"{kkPageUrlStart}/contest/log_list_json/{contestId}/";
        }

        public HttpRequestMessage GetHeaders(List<string> tokens, string loginPageUrl)
        {
            var httpRequestMessage = new HttpRequestMessage();

            httpRequestMessage.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpRequestMessage.Headers.Add("Cookie", string.Join(";", tokens));
            httpRequestMessage.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            httpRequestMessage.Headers.Referrer = new Uri(loginPageUrl);
            httpRequestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_12_5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");

            return httpRequestMessage;
        }

        public List<string> ToColumns(HtmlNode elem)
        {
            var childrenTexts = new List<string>();

            foreach (var child in elem.ChildNodes)
            {
                childrenTexts.Add(child.InnerText);
            }

            return childrenTexts;
        }

        public async Task<string> GetKkLoginTokenAsync()
        {
            var response = await httpClient.GetAsync(loginPageUrl);
            var htmlContent = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            var loginFormNode = htmlDocument.DocumentNode.SelectSingleNode("//form");
            var loginFormHtml = loginFormNode.InnerHtml;

            var loginFormTokenStart = "value=\'";
            var loginFormTokenEnd = "\'>";

            var csrfTokenStartIndex = loginFormHtml.IndexOf(loginFormTokenStart) + loginFormTokenStart.Length;
            var csrfTokenEndIndex = loginFormHtml.IndexOf(loginFormTokenEnd, csrfTokenStartIndex);

            var csrfToken = loginFormHtml.Substring(csrfTokenStartIndex, csrfTokenEndIndex - csrfTokenStartIndex);

            return csrfToken;
        }

        public async Task<bool> DoKkLoginAsync(string username, string password, string csrfToken)
        {
            var body = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("csrfmiddlewaretoken", csrfToken),
            new KeyValuePair<string, string>("next", ""),
        });

            httpClient.DefaultRequestHeaders.Add("Cookie", $"csrftoken={csrfToken}");

            var response = await httpClient.PostAsync(loginPageUrl, body);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (responseContent.Contains(loginErrorReply))
            {
                throw new HttpRequestException("Request failed: " + response.Headers.ToString());
            }

            return true;
        }

        /// <summary>
        /// Updates information in the user log.
        /// </summary>
        /// <param name="contestId">The contest id.</param>
        /// <param name="kmDate">The date in format YYYY-mm-dd.</param>
        /// <param name="kmAmount">The kilometers to log for that day.</param>
        /// <returns></returns>
        /// <exception cref="HttpRequestException"></exception>
        public async Task<int> UpdateLogAsync(string contestId, string kmDate, int kmAmount)
        {
            var csrfToken = await GetKkLoginTokenAsync();

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("contest_id", contestId),
                new KeyValuePair<string, string>("km_date", kmDate),
                new KeyValuePair<string, string>("km_amount", kmAmount.ToString()),
                new KeyValuePair<string, string>("csrfmiddlewaretoken", csrfToken),
            });

            httpClient.DefaultRequestHeaders.Add("Cookie", $"csrftoken={csrfToken}");

            var response = await httpClient.PostAsync(updateLogPageUrl, body);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("Request failed: " + response);
            }

            return (int)response.StatusCode;
        }

        public async Task<User> FetchProfilePageAsync()
        {
            var response = await httpClient.GetAsync(profilePageUrl);
            var htmlContent = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            var requireSignInNode = htmlDocument.DocumentNode.SelectSingleNode("//*[@id='signup']");

            if (requireSignInNode != null)
            {
                throw new HttpRequestException("Login failed");
            }

            var firstName = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='first_name']").GetAttributeValue("value", "");
            var lastName = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='last_name']").GetAttributeValue("value", "");
            var email = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='email']").GetAttributeValue("value", "");
            var nickname = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='nickname']").GetAttributeValue("value", "");

            return new User
            {
                Nickname = nickname,
                Firstname = firstName,
                Lastname = lastName,
                Email = email,
                Municipality = "" // Update with appropriate logic for Municipality
            };
        }

        /// <summary>
        /// Promise of the user profile info.
        /// </summary>
        /// <param name="username">The username to log in with.</param>
        /// <param name="password">The password to log in with.</param>
        /// <returns>Promise of the user profile info.</returns>
        public async Task<User> LoginAsync(string username, string password)
        {
            var csrfToken = await GetKkLoginTokenAsync();
            await DoKkLoginAsync(username, password, csrfToken);
            return await FetchProfilePageAsync();
        }

        public string GetDataUrl(string contestId, int year)
        {
            var start = new DateTimeOffset(new DateTime(year, 1, 1)).ToUnixTimeSeconds();
            var end = new DateTimeOffset(new DateTime(year, 12, 30)).ToUnixTimeSeconds();

            var parameters = new Dictionary<string, string>()
            {
                { "start", start.ToString() },
                { "end", end.ToString() }
            };

            var queryString = new FormUrlEncodedContent(parameters).ReadAsStringAsync().Result;

            return JsonDataUrlStart(contestId) + "?" + queryString;
        }

        public List<SingleResult> MapUserResults(List<dynamic> results)
        {
            return results.Select(entry => new SingleResult
            {
                Date = entry.start,
                Km = float.Parse(entry.title)
            }).ToList();
        }

        /// <summary>
        /// Fetch yearly user results for the specific year or for the current year.
        /// </summary>
        /// <param name="contestId">The contest id.</param>
        /// <param name="year">The year <YYYY>.</param>
        /// <returns></returns>
        public async Task<List<SingleResult>> GetUserResultsAsync(string contestId, int year)
        {
            var url = GetDataUrl(contestId, year);
            Console.WriteLine(url);
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            dynamic data = await response.Content.ReadAsAsync<dynamic>();
            return MapUserResults(data);
        }

        /// <summary>
        /// Fetch the contests the logged in user has participated in.
        /// </summary>
        /// <returns>List of objects containing fields teamName, contest and time.</returns>
        public async Task<List<TeamResult>> GetContestsAsync()
        {
            var response = await httpClient.GetAsync(myTeamsUrl);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var contestRows = doc.DocumentNode.SelectNodes("//table[@id='teams']/tbody/tr");

            var rows = contestRows.Select(row =>
            {
                var columns = row.SelectNodes("td").Select(td => td.InnerText.Trim()).ToList();
                var link = row.SelectSingleNode("td/a").GetAttributeValue("href", "");

                return new
                {
                    TeamName = columns[0],
                    Contest = columns[1],
                    Time = columns[2],
                    Link = link
                };
            }).ToList();

            var results = new List<TeamResult>();

            foreach (var row in rows)
            {
                var contestNameStart = row.Link.LastIndexOf('/', row.Link.Length - 2);
                var year = row.Contest.Substring(row.Contest.Length - 4);
                var contestId = await GetContestIdAsync("/contests" + row.Link.Substring(contestNameStart) + "teams/");

                results.Add(new TeamResult
                {
                    TeamName = row.TeamName,
                    Contest = row.Contest,
                    Time = row.Time,
                    Link = row.Link,
                    Year = year,
                    ContestId = contestId
                });
            }

            return results;
        }

        public async Task<TeamStatus> FetchTeamResultsAsync(string contest)
        {
            var teamUrl = contest;
            var response = await httpClient.GetAsync(kkPageUrlStart + teamUrl); // Not sure if HttpCompletionOption flag is correct
            var htmlContent = await response.Content.ReadAsStringAsync();
            htmlContent.Normalize(); // I trust this normalize whitespace

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            var widgetHeader = htmlDocument.DocumentNode.Descendants("div")
                .FirstOrDefault(d => d.GetAttributeValue("class", "") == "widget")
                ?.Descendants("h4")
                .FirstOrDefault();
            var teamName = widgetHeader?.InnerText.Trim();

            var teamRank = htmlDocument.DocumentNode.Descendants("div")
                .FirstOrDefault(d => d.GetAttributeValue("class", "") == "team-contest-table")
                ?.Descendants("strong")
                .FirstOrDefault()
                ?.InnerText;

            if (!string.IsNullOrEmpty(teamRank))
            {
                teamRank = string.Join("", teamRank.Where(char.IsDigit));
            }

            var teamRows = htmlDocument.DocumentNode.Descendants("div")
                .FirstOrDefault(d => d.GetAttributeValue("data-slug", "") == "my-team")
                ?.Descendants("tbody")
                .FirstOrDefault()
                ?.ChildNodes
                .Where(n => n.Name == "tr");

            var results = teamRows.Select(elem =>
            {
                // Team admin has a user email column visible
                var emailElem = elem.Descendants("td")
                    .FirstOrDefault(d => d.GetAttributeValue("class", "") == "memberEmail");
                emailElem?.ParentNode.Remove();

                var columns = elem.Descendants("td").Select(td => td.InnerText).ToList();
                return new Result
                {
                    Rank = int.Parse(columns[0]),
                    Name = KilometrikisaBE.StringExtensions.TrimPersonName(columns[1]),
                    Km = float.Parse(KilometrikisaBE.StringExtensions.OnlyNumbers(columns[2])),
                    Days = int.Parse(KilometrikisaBE.StringExtensions.OnlyNumbers(columns[3])),
                };
            }).ToList();
        
            return new TeamStatus
            {
                Name = teamName,
                Rank = teamRank,
                Results = results,
            };
        }

        /// <summary>
        /// Get single page of general team statistics for given contest. 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public async Task<List<TeamInfo>> GetTeamInfoPageAsync(string page, int n = 0)
        {
            Func<string, string> cleanTeamName = (name) => {
                name = name.Replace(" TOP-10", "");
                name = name.Substring(0, name.LastIndexOf('('));
                return name.Trim();
            };

            var pageUrl = page + "&page=" + (n + 1);
            var response = await httpClient.GetAsync(pageUrl);
            var htmlContent = await response.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var resultTable = htmlDoc.DocumentNode.Descendants("table")
                .FirstOrDefault(x => x.HasClass("result-table"));
            var rows = resultTable.Descendants("tr");

            var infos = rows.Select(row => {
                var cells = row.Descendants("td").ToList();
                cells.First().Descendants("div").FirstOrDefault()?.Remove();

                var rank = int.Parse(cells[0].InnerText.Trim());
                var name = cleanTeamName(cells[1].InnerText.Trim());
                var kmpp = float.Parse(KilometrikisaBE.StringExtensions.OnlyNumbers(cells[2].InnerText.Trim()));
                var kmTotal = float.Parse(KilometrikisaBE.StringExtensions.OnlyNumbers(cells[3].InnerText.Trim()));
                var days = float.Parse(KilometrikisaBE.StringExtensions.OnlyNumbers(cells[4].InnerText.Trim()));

                return new TeamInfo
                {
                    Rank = rank,
                    Name = name,
                    Kmpp = kmpp,
                    KmTotal = kmTotal,
                    Days = days
                };
            });

            return infos.ToList();
        }

        public async Task<List<TeamInfo>> GetTeamInfoPagesAsync(string page, int n)
        {
            var tasks = Enumerable.Range(0, n).Select(i => GetTeamInfoPageAsync(page, i)).ToArray();
            var results = await Task.WhenAll(tasks);
            return results.SelectMany(x => x).ToList();
        }

        /// <summary>
        /// Lists all contests that are available on the site.
        /// </summary>
        /// <returns></returns>
        public async Task<List<Contest>> GetAllContestsAsync()
        {
            var response = await httpClient.GetAsync(kkPageUrlStart);
            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var contestsMenu = doc.DocumentNode.Descendants("ul")
                .Skip(1).First()
                .Descendants("ul").Last();
            var contests = contestsMenu.Descendants("li")
                .Select(li => new Contest
                {
                    Name = li.Descendants("a").First().InnerText,
                    Link = li.Descendants("a").First().GetAttributeValue("href", "")
                }).ToList();
            return contests;
        }

        /// <summary>
        /// Get the latest available contest from the site.
        /// </summary>
        /// <returns></returns>
        public async Task<Contest> GetLatestContestAsync()
        {
            var contests = await GetAllContestsAsync();
            return contests[0];
        }

        /// <summary>
        /// Get the internal contest id for the specified contest url (returned by getAllContests).
        /// </summary>
        /// <param name="contestUrl">The contest for which to retrieve internal id.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> GetContestIdAsync(string contestUrl)
        {
            var response = await httpClient.GetAsync(kkPageUrlStart + contestUrl);
            var body = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(body);
            var script = doc.DocumentNode.Descendants("script").FirstOrDefault();
            if (script != null)
            {
                var match = Regex.Match(script.InnerHtml, @"json-search/(\d+)/");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            throw new Exception("Contest ID not found");
        }

        public async Task<string> GetLatestContestIdAsync()
        {
            var latestContest = await GetAllContestsAsync();
            return await GetContestIdAsync(latestContest[0].Link);
        }

        public async Task<string> GetAllTeamsTopListPageAsync()
        {
            var latestContest = await GetLatestContestAsync();
            return kkPageUrlStart + latestContest.Link + "?sort=rank&order=asc";
        }

        public async Task<string> AllTeamsTopListPageAsync()
        {
            var latestContest = await GetLatestContestAsync();
            return kkPageUrlStart + latestContest.Link + "?sort=rank&order=asc";
        }

        public async Task<string> LargeTeamsTopListPageAsync()
        {
            var latestContest = await GetLatestContestAsync();
            return kkPageUrlStart + latestContest.Link + "large/?sort=rank&order=asc";
        }

        public async Task<string> PowerTeamsTopListPageAsync()
        {
            var latestContest = await GetLatestContestAsync();
            return kkPageUrlStart + latestContest.Link + "power/?sort=rank&order=asc";
        }

        public async Task<string> SmallTeamsTopListPageAsync()
        {
            var latestContest = await GetLatestContestAsync();
            return kkPageUrlStart + latestContest.Link + "small/?sort=rank&order=asc";
        }


    }
}