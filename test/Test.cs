using System;
using LinkRadio;
class T {
  static int fails=0;
  static void Eq(object a, object b, string n){ if(!object.Equals(a,b)){ fails++; Console.WriteLine("FAIL "+n+": got ["+a+"] expected ["+b+"]"); } else Console.WriteLine("ok   "+n); }
  static void Main(){
    Uri u;
    Eq(Util.TryParseHttpUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1&t=3",out u),true,"parse yt");
    Eq(Util.Normalize(u),"https://www.youtube.com/watch?v=dQw4w9WgXcQ","normalize strips list");
    Eq(Util.KeyFor(u),"yt_dQw4w9WgXcQ","key");
    Util.TryParseHttpUrl("https://youtu.be/dQw4w9WgXcQ?si=abc",out u); Eq(Util.KeyFor(u),"yt_dQw4w9WgXcQ","youtu.be key");
    Util.TryParseHttpUrl("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=RDAMVM",out u); Eq(Util.KeyFor(u),"yt_dQw4w9WgXcQ","ytmusic key");
    Eq(Util.HostAllowed(u,"youtube.com,youtu.be"),true,"ytmusic host allowed");
    Util.TryParseHttpUrl("https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ",out u); Eq(Util.HostAllowed(u,"youtube.com,youtu.be"),false,"lookalike host blocked");
    Util.TryParseHttpUrl("https://evil.com/youtube.com",out u); Eq(Util.HostAllowed(u,"youtube.com"),false,"path trick blocked");
    Util.TryParseHttpUrl("https://www.youtube.com/playlist?list=PLabc123XYZ_-9&si=x",out u); Eq(Util.YouTubePlaylistId(u),"PLabc123XYZ_-9","playlist id");
    Util.TryParseHttpUrl("https://music.youtube.com/playlist?list=OLAK5uy_abc",out u); Eq(Util.YouTubePlaylistId(u),"OLAK5uy_abc","ytmusic playlist id");
    Util.TryParseHttpUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL1abc",out u); Eq(Util.YouTubePlaylistId(u),null,"watch+list is single video");
    Util.TryParseHttpUrl("https://evil.com/playlist?list=PLabc123",out u); Eq(Util.YouTubePlaylistId(u),null,"foreign host no playlist");
    Util.TryParseHttpUrl("https://www.youtube.com/playlist?list=PL1%22%20--exec",out u); Eq(Util.YouTubePlaylistId(u),null,"playlist injection blocked");
    { string sp; bool un;
      Eq(Util.TryParseSpotifyTrack("https://spotify.link/AbCd1234xY",out sp,out un),true,"spotify.link accepted"); Eq(sp,"https://spotify.link/AbCd1234xY","spotify.link url");
      Eq(Util.TryParseSpotifyTrack("https://open.spotify.com/intl-de/track/4cOdK2wGLETKBW3PvgPWqT?si=abc",out sp,out un),true,"intl track"); Eq(sp,"https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT","intl track url");
      Eq(Util.FindSpotifyTrackId("<a href=\"https://open.spotify.com/intl-de/track/4cOdK2wGLETKBW3PvgPWqT?si=x\">"),"4cOdK2wGLETKBW3PvgPWqT","find id in html");
      string ti2,ar2;
      Eq(Util.ParseSpotifyHtml("<html><head><title>Mr. Brightside - song and lyrics by The Killers | Spotify</title></head>",out ti2,out ar2),true,"title fmt1"); Eq(ti2+"|"+ar2,"Mr. Brightside|The Killers","fmt1 vals");
      Eq(Util.ParseSpotifyHtml("<head><meta content=\"Song X\" property=\"og:title\"><meta content=\"Listen to Song X on Spotify. Artist Y \u00B7 Song \u00B7 2020\" property=\"og:description\"></head>",out ti2,out ar2),true,"meta reversed order"); Eq(ti2+"|"+ar2,"Song X|Artist Y","meta vals");
      Eq(Util.ParseOEmbedTitle("{\"html\":\"<iframe>\",\"title\":\"Mr. Brightside\",\"type\":\"rich\"}"),"Mr. Brightside","oembed title"); }
    Eq(Util.TryParseHttpUrl("https://youtu.be/x\" --exec calc",out u),false,"quote/space injection blocked");
    Eq(Util.TryParseHttpUrl("-o /etc/passwd",out u),false,"option injection blocked");
    Eq(Util.TryParseHttpUrl("file:///c:/x.mp3",out u),false,"file scheme blocked");
    string tu; bool unsup;
    Eq(Util.TryParseSpotifyTrack("https://open.spotify.com/intl-de/track/4cOdK2wGLETKBW3PvgPWqT?si=abc",out tu,out unsup),true,"spotify track");
    Eq(tu,"https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT","spotify canonical");
    Eq(Util.TryParseSpotifyTrack("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M",out tu,out unsup),false,"playlist not track");
    Eq(unsup,true,"playlist flagged unsupported");
    Eq(Util.TryParseSpotifyTrack("spotify:track:4cOdK2wGLETKBW3PvgPWqT",out tu,out unsup),true,"spotify uri");
    string ti,ar;
    Eq(Util.ParseSpotifyHtml("<html><head><title>Blinding Lights - song and lyrics by The Weeknd | Spotify</title></head>",out ti,out ar),true,"html en");
    Eq(ti+"|"+ar,"Blinding Lights|The Weeknd","html en values");
    Eq(Util.ParseSpotifyHtml("<title>Song - Remastered - Song von Foo &amp; Bar | Spotify</title>",out ti,out ar),true,"html de");
    Eq(ti+"|"+ar,"Song - Remastered|Foo & Bar","html de values");
    Eq(Util.ParseSpotifyHtml("<meta property=\"og:title\" content=\"After\"/><meta property=\"og:description\" content=\"Artist X \u00B7 Album \u00B7 Song \u00B7 2020\"/>",out ti,out ar),true,"og fallback");
    Eq(ti+"|"+ar,"After|Artist X","og values");
    Eq(Util.CleanQuery("a\"b\\c\nd"),"abcd","cleanquery");
    Eq(Util.FormatTime(75.4),"1:15","time");
    { string n1,ti3,wh;
      Eq(Util.ParseQueueLine("2. a-ha - Take On Me  [Florian]",out n1,out ti3,out wh),true,"queue line"); Eq(n1+"|"+ti3+"|"+wh,"2|a-ha - Take On Me|Florian","queue line vals");
      Eq(Util.ParseQueueLine("10. 1. Song [x]  [Max]",out n1,out ti3,out wh),true,"queue line brackets"); Eq(n1+"|"+ti3+"|"+wh,"10|1. Song [x]|Max","queue brackets vals");
      Eq(Util.ParseQueueLine("+ 4 more",out n1,out ti3,out wh),false,"queue more line");
      Eq(Util.CountQueue("1. A  [x]\n2. B  [y]\n+ 4 more"),6,"queue count");
      Eq(Util.CountQueue(""),0,"queue count empty");
      Eq(Util.ParseSeconds("242.5"),242.5f,"seconds"); Eq(Util.ParseSeconds("NA"),0f,"seconds unknown"); Eq(Util.ParseSeconds(null),0f,"seconds null"); }
    Console.WriteLine(fails==0?"ALL OK":("FAILS: "+fails));
    Environment.Exit(fails==0?0:1);
  }
}
