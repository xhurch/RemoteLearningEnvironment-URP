using Services.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UMP.Services.Helpers;
using UnityEngine;
using UnityEngine.Networking;

namespace UMP.Services.Youtube
{
    public class YoutubeService : ServiceBase
    {
        private const string PLAYBACK = "videoplayback";
        private string[] _signatures = { "youtu.be/", "www.youtube", "youtube.com/embed/" };

        public static YoutubeService Default
        {
            get
            {
                return new YoutubeService();
            }
        }

        public override bool ValidUrl(string url)
        {
            foreach (var signature in _signatures)
            {
                if (url.Contains(signature))
                    return true;
            }

            return false;
        }

        public override IEnumerator GetAllVideos(string url, Action<List<Video>> resultCallback, Action<string> errorCallback = null)
        {
            if (!TryNormalize(url, out url))
                throw new ArgumentException("URL is not a valid Youtube URL!");

            var requestText = string.Empty;
#if UNITY_2017_2_OR_NEWER
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", string.Empty);
            yield return request.SendWebRequest();
#else
            var headers = new Dictionary<string, string>();
            headers.Add("User-Agent", string.Empty);
            var request = new WWW(url, null, headers);
            yield return request;
#endif

            if (!string.IsNullOrEmpty(request.error))
            {
                errorCallback(string.Format("[YouTubeService.GetAllVideos] url request is failed: {0}", request.error));
                yield break;
            }

#if UNITY_2017_2_OR_NEWER
            requestText = request.downloadHandler.text;
#else
            requestText = request.text;
#endif

            var ytVideos = new List<YoutubeVideo>();
            yield return ParseVideos(requestText, (videos) => {
                var orderedVideos = from video in videos orderby video.Resolution, video.AudioBitrate select video;
                ytVideos = orderedVideos.ToList();
            }, errorCallback);

            if (resultCallback != null)
                resultCallback(ytVideos.Cast<Video>().ToList());
        }

        private bool TryNormalize(string url, out string normalized)
        {
            normalized = null;

            var builder = new StringBuilder(url);

            url = builder.Replace("youtu.be/", "youtube.com/watch?v=")
                .Replace("youtube.com/embed/", "youtube.com/watch?v=")
                .Replace("/v/", "/watch?v=")
                .Replace("/watch#", "/watch?")
                .ToString();

            var query = new Query(url);
            var value = string.Empty;

            if (!query.TryGetValue("v", out value))
                return false;

            normalized = "https://youtube.com/watch?v=" + value;
            return true;
        }

        private IEnumerator ParseVideos(string source, Action<List<YoutubeVideo>> resultCallback, Action<string> errorCallback = null)
        {
            var videos = new List<YoutubeVideo>();
            var title = string.Empty;
            var jsPlayer = string.Empty;

            try
            {
                title = Regex.Unescape(Json.GetKey("title", source));
                jsPlayer = ParseJsPlayer(source);

                if (string.IsNullOrEmpty(jsPlayer) && resultCallback != null)
                {
                    resultCallback(videos);
                    yield break;
                }

                var playerResponseMap = Json.GetKey("player_response", source);
                var playerResponseJson = new Json(Regex.Unescape(playerResponseMap).Replace(@"\u0026", "&"));
                var playabilityStatus = playerResponseJson["playabilityStatus"];

                if (playabilityStatus.keys.Contains("status"))
                {
                    if (string.Equals(playabilityStatus["status"].str, "error", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception("[YouTubeService.ParseVideos] Video has unavailable stream.");
                    }
                }

                var errorReason = playabilityStatus.keys.Contains("reason") ? playabilityStatus["reason"].str.Trim() : string.Empty;

                if (string.IsNullOrEmpty(errorReason))
                {
                    var videoDetails = playerResponseJson["videoDetails"];
                    var isLiveStream = videoDetails.keys.Contains("isLive") ? string.Equals(videoDetails["isLive"].str, "true", StringComparison.OrdinalIgnoreCase) : false;

                    if (isLiveStream)
                        throw new Exception("[YouTubeService.ParseVideos] This is live stream so unavailable stream.");

                    var map = Json.GetKey("url_encoded_fmt_stream_map", source);

                    if (!string.IsNullOrEmpty(map))
                    {
                        var queries = map.Split(',').Select((query) => Unscramble(query));

                        foreach (var query in queries)
                            videos.Add(new YoutubeVideo(title, query, jsPlayer));
                    }
                    else
                    {
                        var streamObjects = new List<Json>();
                        var streamingData = playerResponseJson["streamingData"];

                        // Extract Muxed streams
                        if (streamingData.keys.Contains("formats"))
                        {
                            var streamFormat = streamingData["formats"].list;
                            if (streamFormat != null)
                            {
                                streamObjects.AddRange(streamFormat);
                            }
                        }

                        // Extract AdaptiveFormat streams
                        if (streamingData.keys.Contains("adaptiveFormats"))
                        {
                            var streamAdaptiveFormats = streamingData["adaptiveFormats"].list;
                            if (streamAdaptiveFormats != null)
                            {
                                streamObjects.AddRange(streamAdaptiveFormats);
                            }
                        }

                        foreach (var item in streamObjects)
                        {
                            if (item.keys.Contains("url"))
                            {
                                var urlValue = item["url"].str;//item.SelectToken("url")?.Value<string>();
                                if (!string.IsNullOrEmpty(urlValue))
                                {
                                    var query = new UnscrambledQuery(urlValue, false, string.Empty);
                                    videos.Add(new YoutubeVideo(title, query, jsPlayer));
                                    continue;
                                }
                            }

                            if (item.keys.Contains("cipher"))
                            {
                                var cipherValue = item["cipher"].str;//item.SelectToken("cipher")?.Value<string>();
                                if (!string.IsNullOrEmpty(cipherValue))
                                {
                                    videos.Add(new YoutubeVideo(title, Unscramble(cipherValue), jsPlayer));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                if (errorCallback != null)
                    errorCallback(error.ToString());
            }

            // adaptive_fmts
            var adaptiveMap = Json.GetKey("adaptive_fmts", source).Trim();

            if (!string.IsNullOrEmpty(adaptiveMap))
            {
                try
                {
                    var queries = adaptiveMap.Split(',').Select((query) => Unscramble(query));

                    foreach (var query in queries)
                        videos.Add(new YoutubeVideo(title, query, jsPlayer));
                }
                catch (Exception error)
                {
                    if (errorCallback != null)
                        errorCallback(error.ToString());
                }
            }
            else
            {
                var dashmpdMap = Json.GetKey("dashmpd", source).Trim();

                if (!string.IsNullOrEmpty(adaptiveMap))
                {
                    dashmpdMap = HttpUtility.UrlDecode(dashmpdMap).Replace(@"\/", "/");

                    var requestText = string.Empty;
#if UNITY_2017_2_OR_NEWER
                    var request = UnityWebRequest.Get(dashmpdMap);
                    yield return request.SendWebRequest();
#else
                    var request = new WWW(dashmpdMap);
                    yield return request;
#endif

                    try
                    {
                        if (!string.IsNullOrEmpty(request.error))
                            throw new Exception(string.Format("[YouTubeService.ParseVideos] dashmpd request is failed: {0}", request.error));

#if UNITY_2017_2_OR_NEWER
                        requestText = request.downloadHandler.text;
#else
                        requestText = request.text;
#endif

                        var manifest = requestText.Replace(@"\/", "/");
                        var uris = HttpUtility.GetUrisFromManifest(manifest);

                        foreach (var v in uris)
                            videos.Add(new YoutubeVideo(title, UnscrambleManifestUrl(v), jsPlayer));
                    }
                    catch (Exception error)
                    {
                        if (errorCallback != null)
                            errorCallback(error.ToString());
                    }
                }
            }

            if (resultCallback != null)
                resultCallback(videos);
        }

        private string ParseJsPlayer(string source)
        {
            var jsPlayer = Json.GetKey("js", source).Replace(@"\/", "/");

            if (string.IsNullOrEmpty(jsPlayer) || jsPlayer.Trim().Length == 0)
                return string.Empty;

            if (jsPlayer.StartsWith("/yts"))
                return string.Format("https://www.youtube.com{0}", jsPlayer);

            // Try to use old implementation
            if (!jsPlayer.StartsWith("http"))
                jsPlayer = string.Format("https:{0}", jsPlayer);

            return jsPlayer;
        }

        // TODO: Consider making this static...
        private UnscrambledQuery Unscramble(string queryString)
        {
            queryString = queryString.Replace(@"\u0026", "&");
            var query = new Query(queryString);
            var url = query["url"];

            var encrypted = false;
            var signature = string.Empty;
            var sp = string.Empty;

            query.TryGetValue("sp", out sp);

            if (query.TryGetValue("s", out signature))
            {
                encrypted = true;
                url += GetSignatureAndHost(signature, query, sp);
            }
            else if (query.TryGetValue("sig", out signature))
                url += GetSignatureAndHost(signature, query);

            url = HttpUtility.UrlDecode(HttpUtility.UrlDecode(url));

            var uriQuery = new Query(url);

            if (!uriQuery.ContainsKey("ratebypass"))
                url += "&ratebypass=yes";

            return new UnscrambledQuery(url, encrypted, sp);
        }

        private string GetSignatureAndHost(string signature, Query query, string sp = null)
        {
            var sigName = sp == null ? "&signature=" : sp;
            var result = string.Format("&{0}={1}", sigName, signature);
            var host = string.Empty;

            if (query.TryGetValue("fallback_host", out host))
                result += "&fallback_host=" + host;

            return result;
        }

        private UnscrambledQuery UnscrambleManifestUrl(string manifestUri)
        {
            var start = manifestUri.IndexOf(PLAYBACK) + PLAYBACK.Length;
            var baseUri = manifestUri.Substring(0, start);
            var parametersString = manifestUri.Substring(start, manifestUri.Length - start);
            var parameters = parametersString.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var builder = new StringBuilder(baseUri);
            builder.Append("?");

            for (var i = 0; i < parameters.Length; i += 2)
            {
                builder.Append(parameters[i]);
                builder.Append('=');
                builder.Append(parameters[i + 1].Replace("%2F", "/"));

                if (i < parameters.Length - 2)
                    builder.Append('&');
            }

            return new UnscrambledQuery(builder.ToString(), false, string.Empty);
        }
    }
}
