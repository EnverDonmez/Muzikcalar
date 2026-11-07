package com.muzikcalar.app;
import org.junit.Test;
import static org.junit.Assert.*;
import java.util.*;
public class SearchParsingTest {
    @Test public void acceptsYoutubeLinksAndRejectsOtherHosts(){assertEquals("n61ULEU7CO0",Track.videoId("https://www.youtube.com/watch?v=n61ULEU7CO0&t=20"));assertEquals("n61ULEU7CO0",Track.videoId("Dinle: https://youtu.be/n61ULEU7CO0"));assertEquals("n61ULEU7CO0",Track.videoId("https://music.youtube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://evilyoutube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://example.com/youtube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://youtube.com/watch?v=n61ULEU7CO01"));}
    @Test public void parsesNestedSearchEscapedTextAndDeduplicates()throws Exception{String video="{\"videoRenderer\":{\"videoId\":\"n61ULEU7CO0\",\"title\":{\"runs\":[{\"text\":\"Şarkı {\\\"canlı\\\"}\"}]},\"ownerText\":{\"runs\":[{\"text\":\"Sanatçı\"}]},\"lengthText\":{\"simpleText\":\"1:02:03\"}}}";List<Track> tracks=Youtube.parseSearch("<script>var ytInitialData = {\"contents\":["+video+","+video+",{\"channelRenderer\":{}}]};more();</script>");assertEquals(1,tracks.size());assertEquals("Şarkı {\"canlı\"}",tracks.get(0).title);assertEquals(3723,tracks.get(0).seconds);assertEquals("Sanatçı",tracks.get(0).artist);}
    @Test public void durationsAndTurkishMetadataSurviveJson()throws Exception{Track before=new Track("n61ULEU7CO0","İçimdeki Şarkı","Çığlık",183,"");Track after=Track.from(before.json());assertEquals(before.title,after.title);assertEquals(before.cover,after.cover);assertEquals("1:02:03",Track.time(3723));assertEquals(0,Youtube.duration("CANLI"));}
}
