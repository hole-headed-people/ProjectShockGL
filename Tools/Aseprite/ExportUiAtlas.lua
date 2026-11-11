local function exportAtlas(sprite, pngPath)
  local regions = {}
  for _, slice in ipairs(sprite.slices) do
    local name = slice.name
    local bounds = slice.bounds
    if not name:find("%S") then
      error("Every slice needs a name.")
    end
    if regions[name] then
      error("Duplicate slice name: " .. name)
    end
    if not bounds or bounds.width <= 0 or bounds.height <= 0 or
       bounds.x < 0 or bounds.y < 0 or
       bounds.x + bounds.width > sprite.width or bounds.y + bounds.height > sprite.height then
      error("Slice lies outside the canvas or has no area: " .. name)
    end
    regions[name] = { X = bounds.x, Y = bounds.y, Width = bounds.width, Height = bounds.height }
  end
  if not next(regions) then
    error("Add at least one named slice before exporting.")
  end

  if pngPath:sub(-4):lower() == ".png" then
    pngPath = pngPath:sub(1, -5)
  end
  local jsonPath = pngPath .. ".json"
  pngPath = pngPath .. ".png"
  local files = { { path = pngPath }, { path = jsonPath } }
  local replacing = app.fs.isFile(pngPath) or app.fs.isFile(jsonPath)
  if replacing and app.alert {
    title = "Replace UI atlas?",
    text = { pngPath, jsonPath },
    buttons = { "Replace", "Cancel" }
  } ~= 1 then
    return
  end

  local suffix = ".atlas-" .. tostring(os.time()) .. "-" .. tostring(math.random(100000, 999999))
  files[1].temp = pngPath .. suffix .. ".png"
  files[2].temp = jsonPath .. suffix
  local ok, message = pcall(function()
    local metadata = json.encode({ Regions = regions }) .. "\n"
    local image = Image(sprite.width, sprite.height, ColorMode.RGB)
    image:drawSprite(sprite, 1)
    image:saveAs(files[1].temp)
    if not app.fs.isFile(files[1].temp) then
      error("Could not export the PNG.")
    end

    local output = assert(io.open(files[2].temp, "wb"))
    local written, writeError = output:write(metadata)
    local closed, closeError = output:close()
    assert(written, writeError)
    assert(closed, closeError)

    for _, file in ipairs(files) do
      if app.fs.isFile(file.path) then
        local backup = file.path .. suffix .. ".bak"
        assert(os.rename(file.path, backup))
        file.backup = backup
      end
      assert(os.rename(file.temp, file.path))
      file.published = true
    end
  end)

  for _, file in ipairs(files) do
    os.remove(file.temp)
    if ok then
      if file.backup then os.remove(file.backup) end
    else
      if file.published then os.remove(file.path) end
      if file.backup and not os.rename(file.backup, file.path) then
        message = tostring(message) .. "\nBackup: " .. file.backup
      end
    end
  end
  if not ok then error(message, 0) end

  app.alert { title = "UI atlas exported", text = { pngPath, jsonPath } }
end

local sprite = app.activeSprite
if not sprite then
  app.alert("Open an Aseprite document first.")
  return
end

local dialog = Dialog("Export UI atlas")
dialog:file {
  id = "png", label = "PNG", save = true, entry = true, filetypes = { "png" },
  filename = app.fs.filePathAndTitle(sprite.filename) .. ".png"
}
dialog:button { id = "export", text = "Export" }
dialog:button { id = "cancel", text = "Cancel" }
dialog:show { wait = true }

local data = dialog.data
if data.export then
  if not data.png or not data.png:find("%S") then
    app.alert("Choose a PNG output file.")
    return
  end
  local ok, message = pcall(exportAtlas, sprite, data.png)
  if not ok then
    app.alert { title = "UI atlas export failed", text = tostring(message) }
  end
end
