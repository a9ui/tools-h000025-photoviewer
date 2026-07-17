'use client';

import React, { useState, useEffect, useId, useRef, useCallback } from 'react';
import { X } from 'lucide-react';
import { useImageStore } from '../store/ImageContext';

interface TagEntry { tag: string; count: number; }

function parseQueryTags(query: string): string[] {
  return query
    .split(',')
    .map((token) => token.trim())
    .filter(Boolean);
}

function chipToneClass(tag: string): string {
  let hash = 0;
  for (let i = 0; i < tag.length; i++) {
    hash = (hash * 31 + tag.charCodeAt(i)) >>> 0;
  }
  return `tone-${hash % 6}`;
}

export default function SearchBar() {
  const { searchQuery, setSearchQuery, indexToken } = useImageStore();
  const [committedTags, setCommittedTags] = useState<string[]>(() => parseQueryTags(searchQuery));
  const [inputToken, setInputToken] = useState('');
  const [tags, setTags] = useState<TagEntry[]>([]);
  const [isLoadingTags, setIsLoadingTags] = useState(true);
  const [suggestions, setSuggestions] = useState<TagEntry[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [selectedIdx, setSelectedIdx] = useState(-1);
  const [draggingIdx, setDraggingIdx] = useState<number | null>(null);
  const [dragOverIdx, setDragOverIdx] = useState<number | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const lastSentQueryRef = useRef(searchQuery);
  const suggestionListboxId = useId();
  const suggestionStatusId = useId();

  const composedQuery = [...committedTags, inputToken.trim()].filter(Boolean).join(', ');

  useEffect(() => {
    if (searchQuery === lastSentQueryRef.current) return;
    setCommittedTags(parseQueryTags(searchQuery));
    setInputToken('');
    setSuggestions([]);
    setShowDropdown(false);
    setSelectedIdx(-1);
    lastSentQueryRef.current = searchQuery;
  }, [searchQuery]);

  useEffect(() => {
    let isCurrent = true;
    const tokenParam = indexToken ? `?indexToken=${encodeURIComponent(indexToken)}` : '';
    fetch(`/api/tags${tokenParam}`)
      .then((r) => r.json())
      .then((data) => {
        if (isCurrent && data.tags) setTags(data.tags);
      })
      .catch(() => {})
      .finally(() => {
        if (isCurrent) setIsLoadingTags(false);
      });
    return () => {
      isCurrent = false;
    };
  }, [indexToken]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (composedQuery === searchQuery || composedQuery === lastSentQueryRef.current) return;
    debounceRef.current = setTimeout(() => {
      if (composedQuery === lastSentQueryRef.current) return;
      lastSentQueryRef.current = composedQuery;
      setSearchQuery(composedQuery);
    }, 200);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [composedQuery, searchQuery, setSearchQuery]);

  useEffect(() => {
    if (!inputToken.trim() || tags.length === 0) {
      setSuggestions([]);
      return;
    }
    const lastToken = inputToken.trim().toLowerCase();
    if (lastToken.length < 1) {
      setSuggestions([]);
      return;
    }

    const matched = tags
      .filter((t) => {
        if (!t.tag.includes(lastToken) || t.tag === lastToken) return false;
        return !committedTags.includes(t.tag);
      })
      .sort((a, b) => {
        const aStarts = a.tag.startsWith(lastToken) ? 0 : 1;
        const bStarts = b.tag.startsWith(lastToken) ? 0 : 1;
        if (aStarts !== bStarts) return aStarts - bStarts;
        return b.count - a.count;
      })
      .slice(0, 8);

    setSuggestions(matched);
    setSelectedIdx(-1);
  }, [inputToken, tags, committedTags]);

  const addTag = useCallback((tag: string) => {
    const parsed = parseQueryTags(tag);
    if (parsed.length === 0) return;

    setCommittedTags((prev) => {
      const next = [...prev];
      for (const item of parsed) {
        if (!next.includes(item)) next.push(item);
      }
      return next;
    });

    setInputToken('');
    setSuggestions([]);
    setShowDropdown(false);
    setSelectedIdx(-1);
    inputRef.current?.focus();
  }, []);

  const removeTag = useCallback((tag: string) => {
    setCommittedTags((prev) => prev.filter((item) => item !== tag));
  }, []);

  const reorderTag = useCallback((from: number, to: number) => {
    if (from === to) return;
    setCommittedTags((prev) => {
      if (from < 0 || to < 0 || from >= prev.length || to >= prev.length) return prev;
      const next = [...prev];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      return next;
    });
  }, []);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (suggestions.length > 0) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        setShowDropdown(true);
        setSelectedIdx((prev) => Math.min(prev + 1, suggestions.length - 1));
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setShowDropdown(true);
        setSelectedIdx((prev) => Math.max(prev - 1, -1));
      } else if (e.key === 'Enter' && selectedIdx >= 0) {
        e.preventDefault();
        addTag(suggestions[selectedIdx].tag);
        return;
      } else if (e.key === 'Tab' && selectedIdx >= 0) {
        e.preventDefault();
        addTag(suggestions[selectedIdx].tag);
        return;
      } else if (e.key === 'Escape') {
        e.preventDefault();
        setShowDropdown(false);
        setSelectedIdx(-1);
        return;
      }
    }

    if (e.key === 'Enter' || e.key === ',') {
      if (inputToken.trim()) {
        e.preventDefault();
        addTag(inputToken);
      }
      return;
    }

    if (e.key === 'Backspace' && !inputToken && committedTags.length > 0) {
      e.preventDefault();
      setCommittedTags((prev) => prev.slice(0, -1));
    }
  };

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (
        dropdownRef.current && !dropdownRef.current.contains(e.target as Node) &&
        inputRef.current && !inputRef.current.contains(e.target as Node)
      ) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const isSuggestionListOpen = showDropdown && suggestions.length > 0;
  const activeSuggestionId = isSuggestionListOpen && selectedIdx >= 0
    ? `${suggestionListboxId}-option-${selectedIdx}`
    : undefined;
  const suggestionStatus = inputToken.trim()
    ? isLoadingTags
      ? 'Loading tag suggestions.'
      : suggestions.length > 0
        ? `${suggestions.length} tag suggestions available. Use the up and down arrow keys to review them.`
        : 'No tag suggestions available.'
    : '';

  return (
    <div className="search-bar-wrapper">
      <div className="search-bar">
        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="11" cy="11" r="8" />
          <line x1="21" y1="21" x2="16.65" y2="16.65" />
        </svg>

        <div className="search-chip-area">
          {committedTags.map((tag, index) => (
            <span
              key={`${tag}-${index}`}
              className={`search-chip ${chipToneClass(tag)} ${draggingIdx !== null && draggingIdx === index ? 'is-dragging' : ''} ${dragOverIdx !== null && dragOverIdx === index ? 'is-drag-over' : ''}`}
              draggable
              onDragStart={(e) => {
                setDraggingIdx(index);
                setDragOverIdx(index);
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', String(index));
              }}
              onDragOver={(e) => {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
                setDragOverIdx(index);
              }}
              onDrop={(e) => {
                e.preventDefault();
                const from = parseInt(e.dataTransfer.getData('text/plain') || '-1', 10);
                reorderTag(from, index);
                setDraggingIdx(null);
                setDragOverIdx(null);
              }}
              onDragEnd={() => {
                setDraggingIdx(null);
                setDragOverIdx(null);
              }}
            >
              <span className="search-chip-label">{tag}</span>
              <button
                className="search-chip-remove"
                type="button"
                onClick={() => removeTag(tag)}
                title={`Remove ${tag}`}
                aria-label={`Remove tag ${tag}`}
              >
                <X size={13} aria-hidden="true" />
              </button>
            </span>
          ))}

          <input
            ref={inputRef}
            type="text"
            className="search-input"
            role="combobox"
            aria-label="Search tags"
            aria-autocomplete="list"
            aria-expanded={isSuggestionListOpen}
            aria-controls={isSuggestionListOpen ? suggestionListboxId : undefined}
            aria-activedescendant={activeSuggestionId}
            aria-describedby={suggestionStatusId}
            placeholder={committedTags.length === 0 ? 'Search tags. Use comma or Enter to add.' : 'Add another tag...'}
            value={inputToken}
            onChange={(e) => {
              setInputToken(e.target.value);
              setShowDropdown(true);
              setSelectedIdx(-1);
            }}
            onFocus={() => {
              if (suggestions.length > 0) setShowDropdown(true);
            }}
            onKeyDown={handleKeyDown}
          />
        </div>

        {composedQuery && (
          <button
            className="search-clear"
            type="button"
            onClick={() => {
              setCommittedTags([]);
              setInputToken('');
              setShowDropdown(false);
            }}
            title="Clear search"
            aria-label="Clear all search tags"
          >
            <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}
      </div>

      <div id={suggestionStatusId} className="search-suggestion-status" role="status" aria-live="polite">
        {suggestionStatus}
      </div>

      {isSuggestionListOpen && (
        <div
          id={suggestionListboxId}
          className="search-dropdown"
          ref={dropdownRef}
          role="listbox"
          aria-label="Tag suggestions"
        >
          {suggestions.map((s, i) => (
            <button
              id={`${suggestionListboxId}-option-${i}`}
              key={s.tag}
              className={`search-suggestion ${i === selectedIdx ? 'selected' : ''}`}
              type="button"
              role="option"
              aria-selected={i === selectedIdx}
              tabIndex={-1}
              onMouseDown={(e) => {
                e.preventDefault();
                addTag(s.tag);
              }}
              onMouseEnter={() => setSelectedIdx(i)}
            >
              <span className="suggestion-tag">{s.tag}</span>
              <span className="suggestion-count">{s.count}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
