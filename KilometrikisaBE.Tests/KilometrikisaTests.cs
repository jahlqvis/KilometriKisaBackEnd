namespace KilometrikisaBE.Tests
{
    using System.Net.Http;
    using Kilometrikisa;
    using static Kilometrikisa.Kilometrikisa;

    [TestClass]
    public class KilometrikisaTest
    {
        const string kktestLogin = "kilometrikisatesti";
        const string kktestPw = "kilometrikisatesti";

        private static Kilometrikisa? client;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            client = new Kilometrikisa();
            var user = await client.LoginAsync(kktestLogin, kktestPw);
            Assert.AreEqual(user.Firstname, "Kilometri");
            Assert.AreEqual(user.Lastname, "Kisa");
        }

        [TestMethod]
        public async Task TestGetAllContests()
        {
            var contests = await client.GetAllContestsAsync();
            Assert.IsTrue(contests.Count > 0);
        }

        [TestMethod]
        public async Task GetUserResults_2017_EverythingIsZero()
        {
            var results = await client.GetUserResultsAsync("22", 2017);
            Assert.IsTrue(results.Count > 50);
            var totalKm = results.Select(r => r.Km).Sum();
            Assert.AreEqual(0, totalKm);
            Console.WriteLine(totalKm + " km ridden");
        }

        [TestMethod]
        public async Task GetUserResults_2021_ReturnsResults()
        {
            var contestId = "45";
            var year = 2021;

            var results = await client.GetUserResultsAsync(contestId, year);

            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 50);

            var totalKm = results.Sum(r => r.Km);

            Assert.IsTrue(totalKm > 0 && totalKm < 500);
            Console.WriteLine(totalKm + " km ridden");
        }

        [TestMethod]
        public async Task UpdateLogTest()
        {
            var contestId = await client.GetLatestContestIdAsync();
            await client.UpdateLogAsync(contestId, "2021-07-17", (int)100.5);
            var results = await client.GetUserResultsAsync(contestId, 2021);
            Assert.IsTrue(results.Count > 15);
            var totalKm = results.Sum(r => r.Km);
            Assert.IsTrue(totalKm > 100);
            Console.WriteLine(totalKm + " km ridden after update!");
            await client.UpdateLogAsync(contestId, "2021-07-17", 0);
        }

        [TestMethod]
        public async Task GetKkLoginTokenTest()
        {
            var token = await client.GetKkLoginTokenAsync();
            Assert.IsTrue(token.Count() == 32);
        }

        [TestMethod]
        public async Task GetTeamInfoPageTest()
        {
            var page = await client.AllTeamsTopListPageAsync();
            var teams = await client.GetTeamInfoPageAsync(page);
            Assert.AreEqual(50, teams.Count);
            Console.WriteLine(teams[0]);
        }

        [TestMethod]
        public async Task GetTeamInfoPagesTest()
        {
            var n = 4;
            var page = await client.AllTeamsTopListPageAsync();
            var teams = await client.GetTeamInfoPagesAsync(page, n);
            Assert.IsTrue(teams.Count > 50);
        }
        
        [TestMethod]
        public async Task FetchProfilePage_Fails()
        {
            try
            {
                var user = await client.FetchProfilePageAsync();
                Assert.Fail("Expected FetchProfilePage to fail, but it did not.");
            }
            catch
            {
                Console.WriteLine("FetchProfilePage failed as expected.");
            }
        }

        [TestMethod]
        public async Task LoginFail()
        {
            try
            {
                await client.LoginAsync("invaliduser", "invalidpw");
                Assert.Fail("Expected exception was not thrown");
            }
            catch
            {
                Console.WriteLine("Login failed as expected");
            }
        }

        [TestMethod]
        public async Task GetContestsTest()
        {
            var result = await client.GetContestsAsync();

            Assert.AreEqual(4, result.Count);

            Assert.AreEqual(new TeamResult
            {
                ContestId = "31",
                Link = "/teams/kesakuntoilijat/kilometrikisa-2018/",
                Contest = "Kilometrikisa 2018",
                TeamName = "Kesäkuntoilijat",
                Time = "01.05.2018 – 22.09.2018",
                Year = "2018"
            }, result[1]);

            Assert.AreEqual(new TeamResult
            {
                ContestId = "30",
                Link = "/teams/talvikisa-2018/talvikilometrikisa-2018/",
                Contest = "Talvikilometrikisa 2018",
                TeamName = "Talvikisa 2018",
                Time = "01.01.2018 – 28.02.2018",
                Year = "2018"
            }, result[2]);
        }

        [TestMethod]
        public async Task FetchTeamTest()
        {
            var contests = await client.GetContestsAsync();
            var teamResults = await client.FetchTeamResultsAsync(contests[0].Contest);

            Assert.AreEqual("2021Testi", teamResults.Name);
            Assert.AreEqual(1, teamResults.Results.Count);
            Assert.AreEqual(1, teamResults.Results[0].Rank);
        }

        [TestMethod]
        public async Task GetAllContestsTest()
        {
            var contests = await client.GetAllContestsAsync();

            Assert.IsTrue(contests.Count > 10);
            var contest = contests.Find(c => c.Name == "Kilometrikisa 2018");

            Assert.IsNotNull(contest);
            Assert.AreEqual("/contests/kilometrikisa-2018/teams/", contest.Link);
        }

        [TestMethod]
        public async Task GetLatestContestTest()
        {
            var contest = await client.GetLatestContestAsync();
            Assert.AreEqual("Kilometrikisa 2021", contest.Name);
            Assert.AreEqual("/contests/kilometrikisa-2021/teams/", contest.Link);
        }

        [TestMethod]
        public async Task TestGetContestId()
        {
            var contestUrl = (await client.GetAllContestsAsync()).First().Link;
            var contestId = await client.GetContestIdAsync(contestUrl);
            Assert.AreEqual("45", contestId);
        }
    }
}