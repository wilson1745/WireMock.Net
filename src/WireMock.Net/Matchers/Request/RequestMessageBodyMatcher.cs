using JetBrains.Annotations;
using System;
using System.Linq;
using WireMock.Types;
using WireMock.Util;
using WireMock.Validation;

namespace WireMock.Matchers.Request
{
    /// <summary>
    /// The request body matcher.
    /// </summary>
    public class RequestMessageBodyMatcher : IRequestMatcher
    {
        /// <summary>
        /// The body function
        /// </summary>
        public Func<string, bool> Func { get; }

        /// <summary>
        /// The body data function for byte[]
        /// </summary>
        public Func<byte[], bool> DataFunc { get; }

        /// <summary>
        /// The body data function for json
        /// </summary>
        public Func<object, bool> JsonFunc { get; }

        /// <summary>
        /// The body data function for BodyData
        /// </summary>
        public Func<IBodyData, bool> BodyDataFunc { get; }

        /// <summary>
        /// The matchers.
        /// </summary>
        public IMatcher[] Matchers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="matchBehaviour">The match behaviour.</param>
        /// <param name="body">The body.</param>
        public RequestMessageBodyMatcher(MatchBehaviour matchBehaviour, [NotNull] string body) : this(new[] { new WildcardMatcher(matchBehaviour, body) }.Cast<IMatcher>().ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="matchBehaviour">The match behaviour.</param>
        /// <param name="body">The body.</param>
        public RequestMessageBodyMatcher(MatchBehaviour matchBehaviour, [NotNull] byte[] body) : this(new[] { new ExactObjectMatcher(matchBehaviour, body) }.Cast<IMatcher>().ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="matchBehaviour">The match behaviour.</param>
        /// <param name="body">The body.</param>
        public RequestMessageBodyMatcher(MatchBehaviour matchBehaviour, [NotNull] object body) : this(new[] { new ExactObjectMatcher(matchBehaviour, body) }.Cast<IMatcher>().ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="func">The function.</param>
        public RequestMessageBodyMatcher([NotNull] Func<string, bool> func)
        {
            Check.NotNull(func, nameof(func));
            Func = func;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="func">The function.</param>
        public RequestMessageBodyMatcher([NotNull] Func<byte[], bool> func)
        {
            Check.NotNull(func, nameof(func));
            DataFunc = func;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="func">The function.</param>
        public RequestMessageBodyMatcher([NotNull] Func<object, bool> func)
        {
            Check.NotNull(func, nameof(func));
            JsonFunc = func;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="func">The function.</param>
        public RequestMessageBodyMatcher([NotNull] Func<IBodyData, bool> func)
        {
            Check.NotNull(func, nameof(func));
            BodyDataFunc = func;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestMessageBodyMatcher"/> class.
        /// </summary>
        /// <param name="matchers">The matchers.</param>
        public RequestMessageBodyMatcher([NotNull] params IMatcher[] matchers)
        {
            Check.NotNull(matchers, nameof(matchers));
            Matchers = matchers;
        }

        /// <see cref="IRequestMatcher.GetMatchingScore"/>
        public double GetMatchingScore(IRequestMessage requestMessage, RequestMatchResult requestMatchResult)
        {
            double score = CalculateMatchScore(requestMessage);
            return requestMatchResult.AddScore(GetType(), score);
        }

        private double CalculateMatchScore(IRequestMessage requestMessage, IMatcher matcher)
        {
            if (matcher is NotNullOrEmptyMatcher notNullOrEmptyMatcher)
            {
                switch (requestMessage?.BodyData?.DetectedBodyType)
                {
                    case BodyType.Json:
                    case BodyType.String:
                        return notNullOrEmptyMatcher.IsMatch(requestMessage.BodyData.BodyAsString);

                    case BodyType.Bytes:
                        return notNullOrEmptyMatcher.IsMatch(requestMessage.BodyData.BodyAsBytes);

                    default:
                        return MatchScores.Mismatch;
                }
            }

            if (matcher is ExactObjectMatcher exactObjectMatcher)
            {
                // If the body is a byte array, try to match.
                var detectedBodyType = requestMessage?.BodyData?.DetectedBodyType;
                if (detectedBodyType == BodyType.Bytes || detectedBodyType == BodyType.String)
                {
                    return exactObjectMatcher.IsMatch(requestMessage.BodyData.BodyAsBytes);
                }
            }

            // Check if the matcher is a IObjectMatcher
            if (matcher is IObjectMatcher objectMatcher)
            {
                // If the body is a JSON object, try to match.
                if (requestMessage?.BodyData?.DetectedBodyType == BodyType.Json)
                {
                    return objectMatcher.IsMatch(requestMessage.BodyData.BodyAsJson);
                }

                // If the body is a byte array, try to match.
                if (requestMessage?.BodyData?.DetectedBodyType == BodyType.Bytes)
                {
                    return objectMatcher.IsMatch(requestMessage.BodyData.BodyAsBytes);
                }
            }

            // Check if the matcher is a IStringMatcher
            if (matcher is IStringMatcher stringMatcher)
            {
                // If the body is a Json or a String, use the BodyAsString to match on.
                if (requestMessage?.BodyData?.DetectedBodyType == BodyType.Json || requestMessage?.BodyData?.DetectedBodyType == BodyType.String)
                {
                    return stringMatcher.IsMatch(requestMessage.BodyData.BodyAsString);
                }
            }

            return MatchScores.Mismatch;
        }

        private double CalculateMatchScore(IRequestMessage requestMessage)
        {
            if (Matchers != null && Matchers.Any())
            {
                return Matchers.Max(matcher => CalculateMatchScore(requestMessage, matcher));
            }

            if (Func != null)
            {
                return MatchScores.ToScore(Func(requestMessage?.BodyData?.BodyAsString));
            }

            if (JsonFunc != null)
            {
                return MatchScores.ToScore(JsonFunc(requestMessage?.BodyData?.BodyAsJson));
            }

            if (DataFunc != null)
            {
                return MatchScores.ToScore(DataFunc(requestMessage?.BodyData?.BodyAsBytes));
            }

            if (BodyDataFunc != null)
            {
                return MatchScores.ToScore(BodyDataFunc(requestMessage?.BodyData));
            }

            return MatchScores.Mismatch;
        }
    }
}