/**
 * The query string as it is now, for building the next one.
 *
 * React Router's `setSearchParams` hands its callback the params of the last render, and a page's own `searchParams`
 * are as old. Two writes in one tick — the search box's delayed commit and a filter picked at that moment — would
 * each start from the old URL, and the second would undo the first: the box cleared, the old `q` back in the URL
 * (QA FE-02). `<BrowserRouter>` writes the URL synchronously on navigate, so reading it here sees the first write.
 */
export function latestSearchParams() {
  return new URLSearchParams(window.location.search)
}
