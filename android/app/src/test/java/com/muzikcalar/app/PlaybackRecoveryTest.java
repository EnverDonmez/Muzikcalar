package com.muzikcalar.app;

import androidx.media3.common.PlaybackException;
import org.junit.Test;
import static org.junit.Assert.*;

public final class PlaybackRecoveryTest {
    @Test public void signedUrlAndTransientHttpErrorsCanRetry() {
        for(int code:new int[]{403,408,429,500,502,503,599})assertTrue(PlaybackRecovery.retryStatus(code));
        for(int code:new int[]{200,401,404,410,451})assertFalse(PlaybackRecovery.retryStatus(code));
    }

    @Test public void resolverClassificationDistinguishesNetworkAndUnavailableMedia() {
        PlaybackException network=new PlaybackException("network",null,PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED);
        assertTrue(PlaybackRecovery.shouldRetry(network));
        PlaybackException gone=new PlaybackException("unavailable",new Youtube.SourceFailure("private",false,null),PlaybackException.ERROR_CODE_IO_UNSPECIFIED);
        assertFalse(PlaybackRecovery.shouldRetry(gone));
        PlaybackException expired=new PlaybackException("expired",new Youtube.SourceFailure("HTTP 403",true,null),PlaybackException.ERROR_CODE_IO_UNSPECIFIED);
        assertTrue(PlaybackRecovery.shouldRetry(expired));
    }
}
