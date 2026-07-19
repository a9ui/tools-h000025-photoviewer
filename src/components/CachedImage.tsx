'use client';

/* eslint-disable @next/next/no-img-element -- This component owns cancellable blob/object-URL caching and fallback recovery for local files; next/image cannot preserve that lifecycle. */

import React, { useEffect, useRef, useState } from 'react';
import {
  evictCachedImageUrl,
  getCachedImageUrl,
  isImageSessionExpiredError,
  loadCancellableCachedImageUrl,
} from '../lib/clientImageCache';

type CacheKind = 'thumb' | 'display';

type CachedImageProps = Omit<React.ImgHTMLAttributes<HTMLImageElement>, 'src'> & {
  src: string;
  requestSrc?: string;
  fallbackSrc?: string;
  cacheKind: CacheKind;
  onSessionExpired?: () => void;
};

const TRANSPARENT_IMAGE_DATA_URL = 'data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=';

function resolveImageUrl(value: string) {
  try {
    return new URL(value, window.location.href).href;
  } catch {
    return value;
  }
}

function applyFallbackImage(event: React.SyntheticEvent<HTMLImageElement>, fallbackSrc?: string) {
  if (!fallbackSrc) return false;
  const currentSrc = event.currentTarget.currentSrc || event.currentTarget.src;
  if (resolveImageUrl(currentSrc) === resolveImageUrl(fallbackSrc)) return false;
  event.currentTarget.src = fallbackSrc;
  return true;
}

function useResolvedFallbackSrc(src: string, requestSrc: string | undefined, fallbackSrc: string | undefined) {
  const [failedSrc, setFailedSrc] = useState('');

  useEffect(() => {
    setFailedSrc('');
  }, [requestSrc, src]);

  const markFailed = (value: string) => {
    setFailedSrc(resolveImageUrl(value));
  };

  const isFailed = failedSrc && (
    failedSrc === resolveImageUrl(src) ||
    failedSrc === resolveImageUrl(requestSrc ?? src)
  );

  return {
    shouldUseFallback: Boolean(isFailed && fallbackSrc),
    markFailed,
  };
}

export default function CachedImage({
  src,
  requestSrc,
  fallbackSrc,
  cacheKind,
  alt,
  onError,
  onSessionExpired,
  ...props
}: CachedImageProps) {
  if (cacheKind === 'thumb') {
    const directSrc = requestSrc ?? src;
    return <DirectCachedImage {...props} src={src} requestSrc={requestSrc} fallbackSrc={fallbackSrc} alt={alt} onError={onError} directSrc={directSrc} />;
  }

  return (
    <BlobCachedImage
      {...props}
      src={src}
      requestSrc={requestSrc}
      fallbackSrc={fallbackSrc}
      cacheKind={cacheKind}
      alt={alt}
      onError={onError}
      onSessionExpired={onSessionExpired}
    />
  );
}

function DirectCachedImage({
  directSrc,
  src,
  requestSrc,
  fallbackSrc,
  alt,
  onError,
  ...props
}: Omit<CachedImageProps, 'cacheKind'> & { directSrc: string }) {
  const { shouldUseFallback, markFailed } = useResolvedFallbackSrc(src, requestSrc, fallbackSrc);
  const renderedSrc = shouldUseFallback ? fallbackSrc ?? directSrc : directSrc;

  return (
    <img
      {...props}
      alt={alt ?? ''}
      src={renderedSrc}
      onError={(event) => {
        const currentSrc = event.currentTarget.currentSrc || event.currentTarget.src || renderedSrc;
        markFailed(currentSrc);
        if (applyFallbackImage(event, fallbackSrc)) return;
        onError?.(event);
      }}
    />
  );
}

function BlobCachedImage({
  src,
  requestSrc,
  fallbackSrc,
  cacheKind,
  alt,
  onError,
  onSessionExpired,
  ...props
}: CachedImageProps) {
  const { shouldUseFallback, markFailed } = useResolvedFallbackSrc(src, requestSrc, fallbackSrc);
  const [renderedSrc, setRenderedSrc] = useState<string | null>(() => getCachedImageUrl(src, cacheKind));
  const [sessionExpired, setSessionExpired] = useState(false);
  const sessionExpiredNotifiedRef = useRef(false);
  const onSessionExpiredRef = useRef(onSessionExpired);

  useEffect(() => {
    onSessionExpiredRef.current = onSessionExpired;
  }, [onSessionExpired]);

  useEffect(() => {
    let cancelled = false;
    sessionExpiredNotifiedRef.current = false;
    setSessionExpired(false);
    const cached = getCachedImageUrl(src, cacheKind);
    if (cached) {
      setRenderedSrc(cached);
      return () => {
        cancelled = true;
      };
    }

    setRenderedSrc(null);
    const load = loadCancellableCachedImageUrl(src, requestSrc ?? src, cacheKind);
    load.promise
      .then((objectUrl) => {
        if (!cancelled) setRenderedSrc(objectUrl);
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        if (isImageSessionExpiredError(error)) {
          setRenderedSrc(null);
          setSessionExpired(true);
          if (!sessionExpiredNotifiedRef.current) {
            sessionExpiredNotifiedRef.current = true;
            onSessionExpiredRef.current?.();
          }
          return;
        }
        setRenderedSrc(fallbackSrc ?? src);
      });

    return () => {
      cancelled = true;
      load.cancel();
    };
  }, [cacheKind, fallbackSrc, requestSrc, src]);

  const immediateSrc = sessionExpired
    ? TRANSPARENT_IMAGE_DATA_URL
    : shouldUseFallback ? fallbackSrc ?? src : renderedSrc ?? requestSrc ?? src;

  return (
    <img
      {...props}
      alt={alt ?? ''}
      src={immediateSrc}
      data-image-session-expired={sessionExpired ? 'true' : undefined}
      onError={(event) => {
        const currentSrc = event.currentTarget.currentSrc || event.currentTarget.src || immediateSrc;
        if (renderedSrc?.startsWith('blob:') && resolveImageUrl(currentSrc) === resolveImageUrl(renderedSrc)) {
          evictCachedImageUrl(src, cacheKind);
          setRenderedSrc(null);
        }
        markFailed(currentSrc);
        if (applyFallbackImage(event, fallbackSrc)) return;
        onError?.(event);
      }}
    />
  );
}
