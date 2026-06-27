'use client';

import React, { useEffect, useState } from 'react';
import { evictCachedImageUrl, getCachedImageUrl, loadCancellableCachedImageUrl } from '../lib/clientImageCache';

type CacheKind = 'thumb' | 'display';

type CachedImageProps = Omit<React.ImgHTMLAttributes<HTMLImageElement>, 'src'> & {
  src: string;
  requestSrc?: string;
  fallbackSrc?: string;
  cacheKind: CacheKind;
};

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
  ...props
}: CachedImageProps) {
  const { shouldUseFallback, markFailed } = useResolvedFallbackSrc(src, requestSrc, fallbackSrc);
  const [renderedSrc, setRenderedSrc] = useState<string | null>(() => getCachedImageUrl(src, cacheKind));

  useEffect(() => {
    let cancelled = false;
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
      .catch(() => {
        if (!cancelled) setRenderedSrc(fallbackSrc ?? src);
      });

    return () => {
      cancelled = true;
      load.cancel();
    };
  }, [cacheKind, fallbackSrc, requestSrc, src]);

  const immediateSrc = shouldUseFallback ? fallbackSrc ?? src : renderedSrc ?? requestSrc ?? src;

  return (
    <img
      {...props}
      alt={alt ?? ''}
      src={immediateSrc}
      onError={(event) => {
        const currentSrc = event.currentTarget.currentSrc || event.currentTarget.src || immediateSrc;
        if (renderedSrc && resolveImageUrl(currentSrc) === resolveImageUrl(renderedSrc)) {
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
