package com.muzikcalar.app;
import org.junit.Test;
import static org.junit.Assert.*;
import java.util.*;
public class SearchParsingTest {
    @Test public void acceptsYoutubeLinksAndRejectsOtherHosts(){assertEquals("n61ULEU7CO0",Track.videoId("https://www.youtube.com/watch?v=n61ULEU7CO0&t=20"));assertEquals("n61ULEU7CO0",Track.videoId("Dinle: https://youtu.be/n61ULEU7CO0"));assertEquals("n61ULEU7CO0",Track.videoId("https://music.youtube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://evilyoutube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://example.com/youtube.com/watch?v=n61ULEU7CO0"));assertNull(Track.videoId("https://youtube.com/watch?v=n61ULEU7CO01"));}
    @Test public void parsesNestedSearchEscapedTextAndDeduplicates()throws Exception{String video="{\"videoRenderer\":{\"videoId\":\"n61ULEU7CO0\",\"title\":{\"runs\":[{\"text\":\"Şarkı {\\\"canlı\\\"}\"}]},\"ownerText\":{\"runs\":[{\"text\":\"Sanatçı\"}]},\"lengthText\":{\"simpleText\":\"1:02:03\"}}}";List<Track> tracks=Youtube.parseSearch("<script>var ytInitialData = {\"contents\":["+video+","+video+",{\"channelRenderer\":{}}]};more();</script>");assertEquals(1,tracks.size());assertEquals("Şarkı {\"canlı\"}",tracks.get(0).title);assertEquals(3723,tracks.get(0).seconds);assertEquals("Sanatçı",tracks.get(0).artist);}
    @Test public void durationsAndTurkishMetadataSurviveJson()throws Exception{Track before=new Track("n61ULEU7CO0","İçimdeki Şarkı","Çığlık",183,"");Track after=Track.from(before.json());assertEquals(before.title,after.title);assertEquals(before.cover,after.cover);assertEquals("1:02:03",Track.time(3723));assertEquals(0,Youtube.duration("CANLI"));}

    @Test public void parsesAndLimitsSuggestionsInProviderOrder() throws Exception {
        String response="[\"gülşen\",[null,1,\"\",\"gülşen\",\"gülşen bangır bangır\",\"GÜLŞEN bangır bangır\","
                +"\" gülşen of of \",\"gülşen irgalamaz beni\",\"gülşen dan dan\",\"gülşen canlı\",\"gülşen konser\"]]";
        assertEquals(Arrays.asList("gülşen bangır bangır","gülşen of of","gülşen irgalamaz beni","gülşen dan dan","gülşen canlı"),
                Youtube.parseSuggestions(response,"gülşen"));
        assertTrue(Youtube.parseSuggestions("[\"gülşen\",{}]","gülşen").isEmpty());
    }

    @Test public void acceptsEmbedLinksLikeWindows() {
        assertEquals("n61ULEU7CO0",Track.videoId("https://www.youtube.com/embed/n61ULEU7CO0?start=5"));
        assertNull(Track.videoId("https://example.com/embed/n61ULEU7CO0"));
    }

    @Test public void skipsSponsoredSearchEntries() throws Exception {
        String sponsored="{\"adSlotRenderer\":{\"fulfillmentContent\":{\"videoRenderer\":{\"videoId\":\"aaaaaaaaaaa\","
                +"\"title\":{\"simpleText\":\"Reklam\"}}}}}";
        String promoted="{\"promotedVideoRenderer\":{\"videoRenderer\":{\"videoId\":\"bbbbbbbbbbb\","
                +"\"title\":{\"simpleText\":\"Reklam\"}}}}";
        String video="{\"videoRenderer\":{\"videoId\":\"n61ULEU7CO0\",\"title\":{\"simpleText\":\"Şarkı\"}}}";
        List<Track> tracks=Youtube.parseSearch("var ytInitialData = {\"contents\":["+sponsored+","+promoted+","+video+"]};");
        assertEquals(1,tracks.size());
        assertEquals("n61ULEU7CO0",tracks.get(0).id);
    }

    @Test public void coversOnlyLoadFromYoutubeImageHosts() throws Exception {
        String fallback="https://i.ytimg.com/vi/n61ULEU7CO0/hqdefault.jpg";
        String own="https://i.ytimg.com/vi/n61ULEU7CO0/mqdefault.jpg";
        assertEquals(own,new Track("n61ULEU7CO0","Şarkı","Sanatçı",0,own).cover);
        assertEquals("https://i9.ytimg.com/vi/x/y.jpg",new Track("n61ULEU7CO0","Şarkı","Sanatçı",0,"https://i9.ytimg.com/vi/x/y.jpg").cover);
        for(String foreign:new String[]{"https://example.com/pixel.png","http://i.ytimg.com/vi/n61ULEU7CO0/mqdefault.jpg",
                "https://i.ytimg.com.example.com/a.jpg","content://media/1",""})
            assertEquals(fallback,new Track("n61ULEU7CO0","Şarkı","Sanatçı",0,foreign).cover);
        org.json.JSONObject imported=new org.json.JSONObject().put("id","n61ULEU7CO0").put("thumbnail","https://example.com/pixel.png");
        assertEquals(fallback,Track.from(imported).cover);
    }

    @Test public void collectionStartMustMatchTheVisibleRow() {
        Track first=new Track("n61ULEU7CO0","Bir","A",0,""), second=new Track("aaaaaaaaaaa","İki","B",0,"");
        List<Track> visible=Arrays.asList(first,second);
        assertTrue(Library.isAt(visible,1,second));
        assertFalse(Library.isAt(visible,0,second));
        assertFalse(Library.isAt(visible,2,second));
        assertFalse(Library.isAt(visible,-1,first));
    }

    @Test public void onlySuggestsForSearchText() {
        assertTrue(Youtube.canSuggest("gü"));
        assertFalse(Youtube.canSuggest("g"));
        assertFalse(Youtube.canSuggest("Dinle: https://youtu.be/n61ULEU7CO0"));
        assertFalse(Youtube.canSuggest("music.youtube.com/watch"));
    }
}
