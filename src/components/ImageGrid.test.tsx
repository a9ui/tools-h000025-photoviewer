import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { ImageFile } from "../lib/types";
import { useImageStore } from "../store/ImageContext";
import ImageGrid from "./ImageGrid";

vi.mock("../store/ImageContext", () => ({
  useImageStore: vi.fn(),
}));

vi.mock("./CachedImage", () => ({
  default: ({ alt }: { alt: string }) => <span data-image-alt={alt} />,
}));

const firstImage: ImageFile = {
  id: "C:/images/first.png",
  filename: "first.png",
  absolutePath: "C:/images/first.png",
  fileUrl: "/api/image?first",
  displayUrl: "/api/image?first&display=1",
  fullUrl: "/api/image?first&full=1",
  metadata: { prompt: "first prompt", negativePrompt: "", settings: {} },
  createdAt: 1,
  mtime: 1,
};

const secondImage: ImageFile = {
  ...firstImage,
  id: "C:/images/second.png",
  filename: "second.png",
  absolutePath: "C:/images/second.png",
  fileUrl: "/api/image?second",
  displayUrl: "/api/image?second&display=1",
  fullUrl: "/api/image?second&full=1",
};

const selectImage = vi.fn();
const openPreviewTab = vi.fn();
const openModalAtImage = vi.fn();
const cycleFavoriteLevel = vi.fn();
const decreaseFavoriteLevel = vi.fn();
const markImageSeen = vi.fn();
const requestRevealImage = vi.fn();

function createStore(
  viewMode: "grid" | "list" = "grid",
  options: { selectedIds?: string[]; revealImageId?: string | null } = {},
) {
  return {
    searchQuery: "",
    searchResults: [firstImage, secondImage],
    searchTotal: 2,
    isSearching: false,
    ensureSearchRange: vi.fn(),
    selectImage,
    openPreviewTab,
    cycleFavoriteLevel,
    decreaseFavoriteLevel,
    favorites: {},
    view: {
      viewMode,
      thumbSize: 200,
      columns: 0,
      aspectMode: "original",
      displayStyle: "standard",
      sortBy: "newest",
      randomSeed: "",
      dateFrom: "",
      dateTo: "",
      hiddenFolders: [],
      showUnseenMarkers: false,
    },
    setView: vi.fn(),
    selectedIds: options.selectedIds ?? [],
    showFavOnly: false,
    showUnfavOnly: false,
    favoriteFilterLevels: [],
    showEnhancedOnly: false,
    enhancedSourceIds: {},
    closeAllPreviews: vi.fn(),
    setSearchScrollPosition: vi.fn(),
    getSearchScrollPosition: () => null,
    seenImageIds: {},
    markImageSeen,
    revealImageId: options.revealImageId ?? null,
    consumeRevealImage: vi.fn(),
    openModalAtImage,
    modalImageIds: [],
    setModalImageIds: vi.fn(),
    selectedIndex: null,
    setSelectedIndex: vi.fn(),
    requestRevealImage,
    showSettings: false,
    dirPath: "C:/images",
  } as unknown as ReturnType<typeof useImageStore>;
}

function renderGrid() {
  return render(
    <div className="viewer-main">
      <ImageGrid />
    </div>,
  );
}

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    disconnect() {}
  }
  vi.stubGlobal("ResizeObserver", ResizeObserverMock);
  vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.resolve(new Response("{}"))),
  );
  Object.defineProperties(HTMLElement.prototype, {
    clientHeight: { configurable: true, get: () => 640 },
    clientWidth: { configurable: true, get: () => 960 },
  });
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useImageStore).mockReturnValue(createStore());
});

describe("ImageGrid keyboard primary controls", () => {
  it("reaches the first card by keyboard, selects with Space, and opens with Enter", async () => {
    const user = userEvent.setup();
    renderGrid();

    await user.tab();
    const primary = screen.getByRole("button", { name: /select first\.png/i });
    expect(primary).toHaveFocus();
    expect(primary).toHaveAttribute("aria-pressed", "false");

    await user.keyboard(" ");
    expect(markImageSeen).toHaveBeenCalledWith(firstImage.id);
    expect(selectImage).toHaveBeenCalledWith(
      firstImage,
      [firstImage.id, secondImage.id],
      { range: false, toggle: false },
    );

    await user.keyboard("{Enter}");
    expect(openPreviewTab).toHaveBeenCalledWith(firstImage, {
      makeActive: true,
      pin: true,
    });
    expect(openModalAtImage).toHaveBeenCalledWith(firstImage.id, 0, []);
  });

  it("keeps favorite controls independent and preserves Ctrl/Shift selection flags", async () => {
    const user = userEvent.setup();
    renderGrid();

    const favorite = screen.getByRole("button", {
      name: /increase favorite level for first\.png/i,
    });
    await user.click(favorite);
    expect(cycleFavoriteLevel).toHaveBeenCalledWith(firstImage.id);
    expect(markImageSeen).not.toHaveBeenCalled();

    const primary = screen.getByRole("button", { name: /select first\.png/i });
    fireEvent.keyDown(primary, { key: " ", ctrlKey: true });
    expect(selectImage).toHaveBeenLastCalledWith(
      firstImage,
      [firstImage.id, secondImage.id],
      { range: false, toggle: true },
    );
    fireEvent.keyDown(primary, { key: " ", shiftKey: true });
    expect(selectImage).toHaveBeenLastCalledWith(
      firstImage,
      [firstImage.id, secondImage.id],
      { range: true, toggle: false },
    );
  });

  it("uses a group plus sibling primary and favorite buttons in both grid and list views", () => {
    const { rerender } = renderGrid();
    expect(
      screen.getByRole("group", { name: "Image first.png" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /select first\.png/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", {
        name: /increase favorite level for first\.png/i,
      }),
    ).toBeInTheDocument();

    vi.mocked(useImageStore).mockReturnValue(createStore("list"));
    rerender(
      <div className="viewer-main">
        <ImageGrid />
      </div>,
    );
    expect(
      screen.getByRole("group", { name: "Image first.png" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /select first\.png/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", {
        name: /increase favorite level for first\.png/i,
      }),
    ).toBeInTheDocument();
  });

  it("moves the roving primary target with arrows and focuses it after the requested reveal", async () => {
    const user = userEvent.setup();
    const store = createStore("list", { selectedIds: [firstImage.id] });
    vi.mocked(useImageStore).mockReturnValue(store);
    const { rerender } = renderGrid();

    const firstPrimary = screen.getByRole("button", {
      name: /select first\.png/i,
    });
    firstPrimary.focus();
    await user.keyboard("{ArrowDown}");
    expect(requestRevealImage).toHaveBeenCalledWith(secondImage.id);
    expect(
      screen.getByRole("button", { name: /select second\.png/i }),
    ).toHaveAttribute("tabindex", "0");

    vi.mocked(useImageStore).mockReturnValue(
      createStore("list", {
        selectedIds: [secondImage.id],
        revealImageId: secondImage.id,
      }),
    );
    rerender(
      <div className="viewer-main">
        <ImageGrid />
      </div>,
    );
    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /select second\.png/i }),
      ).toHaveFocus();
    });
  });
});
