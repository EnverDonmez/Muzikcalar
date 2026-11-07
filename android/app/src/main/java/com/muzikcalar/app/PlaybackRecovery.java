package com.muzikcalar.app;

import androidx.media3.common.PlaybackException;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.datasource.HttpDataSource;

@UnstableApi
final class PlaybackRecovery {
    static boolean shouldRetry(PlaybackException error) {
        for(Throwable cause=error.getCause();cause!=null;cause=cause.getCause()){
            if(cause instanceof Youtube.SourceFailure)return ((Youtube.SourceFailure)cause).transientFailure;
            if(cause instanceof HttpDataSource.InvalidResponseCodeException)
                return retryStatus(((HttpDataSource.InvalidResponseCodeException)cause).responseCode);
        }
        return error.errorCode==PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED
                ||error.errorCode==PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT;
    }

    static boolean retryStatus(int code) { return code==403||code==408||code==429||code>=500&&code<=599; }
}
