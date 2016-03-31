using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PactNet.Comparers;
using PactNet.Matchers;

namespace PactNet.Mocks.MockHttpService.Comparers
{
    internal class HttpBodyComparer : IHttpBodyComparer
    {
        public ComparisonResult Compare(dynamic expected, dynamic actual, IDictionary<string, IMatcher> matchingRules)
        {
            var result = new ComparisonResult("has a matching body");

            if (expected == null)
            {
                return result;
            }

            if (actual == null)
            {
                result.RecordFailure(new ErrorMessageComparisonFailure("Actual Body is null"));
                return result;
            }

            // Do a simple check to see if body content might be XML; if so, attempt to parse so we know for sure.
            if (IsXml(expected) && IsXml(actual))
            {
                try
                {
                    var expectedObj = ConvertXmlToJsonObj(expected);
                    var actualObj = ConvertXmlToJsonObj(actual);

                    expected = expectedObj;
                    actual = actualObj;
                }
                catch
                {
                    // Parsing may fail, but that's fine - we'll move forward as if the objects weren't XML.
                }
            }

            var expectedToken = JToken.FromObject(expected);
            var actualToken = JToken.FromObject(actual);

            foreach (var rule in matchingRules)
            {
                MatcherResult matchResult = rule.Value.Match(rule.Key, expectedToken, actualToken);

                //TODO: Maybe we should call this a list of differences
                var comparisonFailures = new List<ComparisonFailure>();

                foreach (var failedCheck in matchResult.MatcherChecks.Where(x => x is FailedMatcherCheck).Cast<FailedMatcherCheck>())
                {
                    //TODO: We should be able to generate a better output, as we know exactly the path that failed
                    var comparisonFailure = new DiffComparisonFailure(expectedToken, actualToken);
                    if (comparisonFailures.All(x => x.Result != comparisonFailure.Result))
                    {
                        comparisonFailures.Add(comparisonFailure);
                    }
                }

                foreach (var failure in comparisonFailures)
                {
                    result.RecordFailure(failure);
                }

                //TODO: When more than 1 rule deal with the situation when a success overrides a failure (either more specific rule or order it's applied?)
            }

            return result;
        }

        private static bool IsXml(dynamic value)
        {
            var xmlStr = value as string;
            return !string.IsNullOrEmpty(xmlStr) && xmlStr.TrimStart().StartsWith("<");
        }

        private static object ConvertXmlToJsonObj(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return JsonConvert.DeserializeObject(JsonConvert.SerializeXmlNode(doc));
        }
    }
}