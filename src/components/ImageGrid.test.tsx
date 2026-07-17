import React from "react";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
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
const retrySearch = vi.fn();
const rescanExpiredSearchSession = vi.fn();
const dismissSearchError = vi.fn();
const clearSelection = vi.fn();
let mockClientWidth = 960;
let mockClientHeight = 640;

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
    clearSelection,
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
    <div className="viewer-main" data-testid="viewer-main">
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
    clientHeight: { configurable: true, get: () => mockClientHeight },
    clientWidth: { configurable: true, get: () => mockClientWidth },
  });
});

beforeEach(() => {
  vi.clearAllMocks();
  mockClientWidth = 960;
  mockClientHeight = 640;
  vi.mocked(useImageStore).mockReturnValue(createStore());
});

describe("ImageGrid keyboard primary controls", () => {
  it("announces a search error above retained results and retries without showing the empty state", async () => {
    vi.mocked(useImageStore).mockReturnValue({
      ...createStore(),
      searchError: "Search service temporarily unavailable",
      retrySearch,
      dismissSearchError,
    });

    const user = userEvent.setup();
    renderGrid();

    expect(screen.getByRole("alert")).toHaveTextContent("Search error: Search service temporarily unavailable");
    expect(screen.getByRole("button", { name: /select first\.png/i })).toBeInTheDocument();
    expect(screen.queryByText(/no images match/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Retry the current search" }));
    expect(retrySearch).toHaveBeenCalledTimes(1);
  });

  it('uses Rescan rather than Retry for an expired index session while retaining the gallery', async () => {
    vi.mocked(useImageStore).mockReturnValue({
      ...createStore(),
      searchError: 'This viewer session expired. Scan the folder set again to refresh it.',
      searchErrorKind: 'session-expired',
      retrySearch,
      rescanExpiredSearchSession,
      dismissSearchError,
    });
    const user = userEvent.setup();
    renderGrid();

    expect(screen.getByRole('alert')).toHaveTextContent('Session expired: This viewer session expired.');
    expect(screen.getByRole('button', { name: /select first\.png/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Rescan the current folder set to refresh the viewer session' }));
    expect(rescanExpiredSearchSession).toHaveBeenCalledTimes(1);
    expect(retrySearch).not.toHaveBeenCalled();
  });

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
    act(() => firstPrimary.focus());
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

  it.each(["grid", "list"] as const)("clears selection from %s background but not image controls", (viewMode) => {
    vi.mocked(useImageStore).mockReturnValue(createStore(viewMode, { selectedIds: [firstImage.id] }));
    renderGrid();
    const canvas = screen.getByTestId('image-grid-background');
    fireEvent.click(canvas);
    expect(clearSelection).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: /select first\.png/i }));
    expect(clearSelection).toHaveBeenCalledTimes(1);
  });

  it("keeps Alt+wheel as native scroll and reserves gallery zoom for Ctrl/Cmd+wheel", () => {
    const store = createStore();
    vi.mocked(useImageStore).mockReturnValue(store);
    renderGrid();
    const canvas = screen.getByTestId("image-grid-background");

    const altWheel = new WheelEvent("wheel", {
      altKey: true,
      bubbles: true,
      cancelable: true,
      deltaY: -100,
    });
    fireEvent(canvas, altWheel);
    expect(altWheel.defaultPrevented).toBe(false);
    expect(store.setView).not.toHaveBeenCalled();

    const ctrlWheel = new WheelEvent("wheel", {
      ctrlKey: true,
      bubbles: true,
      cancelable: true,
      deltaY: -100,
    });
    fireEvent(canvas, ctrlWheel);
    expect(ctrlWheel.defaultPrevented).toBe(true);
    expect(store.setView).toHaveBeenCalledWith({ thumbSize: 220 });
  });

  it("captures Ctrl/Cmd wheel only inside the grid and changes thumbnail size", () => {
    const store = createStore();
    vi.mocked(useImageStore).mockReturnValue(store);
    renderGrid();
    const primary = screen.getByRole("button", { name: /select first\.png/i });
    const card = screen.getByRole("group", { name: "Image first.png" });
    expect(card).toHaveAttribute("data-grid-index", "0");

    const wheel = new WheelEvent("wheel", {
      bubbles: true,
      cancelable: true,
      ctrlKey: true,
      deltaY: -120,
    });
    fireEvent(primary, wheel);

    expect(wheel.defaultPrevented).toBe(true);
    expect(store.setView).toHaveBeenCalledWith({ thumbSize: 220 });
  });

  it("keeps the wheel-targeted image at the same viewport offset when zoom changes the column count", async () => {
    mockClientWidth = 900;
    const images = Array.from({ length: 80 }, (_, index): ImageFile => ({
      ...firstImage,
      id: `C:/images/zoom-${index}.png`,
      filename: `zoom-${index}.png`,
      absolutePath: `C:/images/zoom-${index}.png`,
      fileUrl: `/api/image?zoom=${index}`,
      displayUrl: `/api/image?zoom=${index}&display=1`,
      fullUrl: `/api/image?zoom=${index}&full=1`,
    }));
    const initialStore = {
      ...createStore(),
      searchResults: images,
      searchTotal: images.length,
    } as unknown as ReturnType<typeof useImageStore>;
    vi.mocked(useImageStore).mockReturnValue(initialStore);

    const { rerender } = renderGrid();
    const scrollElement = screen.getByTestId("viewer-main");
    Object.defineProperty(scrollElement, "scrollHeight", {
      configurable: true,
      get: () => 20_000,
    });
    scrollElement.scrollTop = 600;
    fireEvent.scroll(scrollElement);

    await waitFor(() => {
      expect(initialStore.setSearchScrollPosition).toHaveBeenCalledWith(
        expect.any(String),
        600,
      );
    });
    const targetBefore = screen.getByRole("group", { name: "Image zoom-10.png" });
    expect(targetBefore).toHaveStyle({ top: "672px" });
    const offsetBefore = Number.parseFloat(targetBefore.style.top) - scrollElement.scrollTop;

    const wheel = new WheelEvent("wheel", {
      bubbles: true,
      cancelable: true,
      ctrlKey: true,
      deltaY: -120,
    });
    fireEvent(
      screen.getByRole("button", { name: /select zoom-10\.png/i }),
      wheel,
    );
    expect(wheel.defaultPrevented).toBe(true);
    expect(initialStore.setView).toHaveBeenCalledWith({ thumbSize: 220 });

    vi.mocked(useImageStore).mockReturnValue({
      ...initialStore,
      view: { ...initialStore.view, thumbSize: 220 },
    });
    rerender(
      <div className="viewer-main" data-testid="viewer-main">
        <ImageGrid />
      </div>,
    );

    await waitFor(() => expect(scrollElement.scrollTop).toBe(1278));
    const targetAfter = screen.getByRole("group", { name: "Image zoom-10.png" });
    expect(targetAfter).toHaveStyle({ top: "1350px" });
    expect(Number.parseFloat(targetAfter.style.top) - scrollElement.scrollTop).toBe(offsetBefore);
  });

  it("leaves native page zoom available outside the grid and in list mode", () => {
    const gridStore = createStore();
    vi.mocked(useImageStore).mockReturnValue(gridStore);
    const { unmount } = render(
      <div>
        <button type="button">Outside gallery</button>
        <div className="viewer-main"><ImageGrid /></div>
      </div>,
    );
    const outsideWheel = new WheelEvent("wheel", {
      bubbles: true,
      cancelable: true,
      ctrlKey: true,
      deltaY: -120,
    });
    fireEvent(screen.getByRole("button", { name: "Outside gallery" }), outsideWheel);
    expect(outsideWheel.defaultPrevented).toBe(false);
    expect(gridStore.setView).not.toHaveBeenCalled();
    unmount();

    const listStore = createStore("list");
    vi.mocked(useImageStore).mockReturnValue(listStore);
    renderGrid();
    const listWheel = new WheelEvent("wheel", {
      bubbles: true,
      cancelable: true,
      ctrlKey: true,
      deltaY: -120,
    });
    fireEvent(screen.getByRole("button", { name: /select first\.png/i }), listWheel);
    expect(listWheel.defaultPrevented).toBe(false);
    expect(listStore.setView).not.toHaveBeenCalled();
  });

  it("does not replace browser native keyboard zoom shortcuts", () => {
    const store = createStore();
    vi.mocked(useImageStore).mockReturnValue(store);
    renderGrid();
    const keydown = new KeyboardEvent("keydown", {
      bubbles: true,
      cancelable: true,
      ctrlKey: true,
      key: "+",
    });
    fireEvent(window, keydown);

    expect(keydown.defaultPrevented).toBe(false);
    expect(store.setView).not.toHaveBeenCalled();
  });
});
