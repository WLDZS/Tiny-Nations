-- Run in Aseprite batch mode. All raster finishing happens in Aseprite.
local root=app.params['root']
if not root then print('Missing root'); return end
local pc=app.pixelColor
local function rgba(r,g,b,a) return pc.rgba(r,g,b,a or 255) end
local palette={
 {61,48,64},{81,65,95},{111,61,86},{148,82,107},
 {73,43,38},{109,64,44},{155,92,53},{192,129,75},
 {185,84,62},{232,120,69},{255,174,114},{255,212,162},
 {167,143,115},{202,179,145},{228,208,172},{244,229,198},
 {255,246,218},{101,199,209},{54,141,171},{197,241,228},
 {141,155,114},{71,122,171},{241,144,58},{255,217,121},
 {49,41,47},{83,75,78},{124,115,112},{170,157,139}
}
local function nearest(v)
 local r,g,b=pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v)
 local best,score=palette[1],1e10
 for _,c in ipairs(palette) do
  local d=(r-c[1])^2+(g-c[2])^2+(b-c[3])^2
  if d<score then best=c;score=d end
 end
 return rgba(best[1],best[2],best[3])
end
local function openImage(name)
 local s=app.open(root..'/sources/'..name..'_source.png')
 local im=Image(s.spec);im:drawSprite(s,1,Point(0,0));s:close();return im
end
local function extract(im,x0,y0,w,h,checker)
 local out=Image(w,h,ColorMode.RGB)
 for y=0,h-1 do for x=0,w-1 do
  local v=im:getPixel(x0+x,y0+y)
  local r,g,b,a=pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v),pc.rgbaA(v)
  local fake=checker and math.max(r,g,b)-math.min(r,g,b)<27 and math.min(r,g,b)>115
  if a>=100 and not fake then out:drawPixel(x,y,v) end
 end end
 return out
end
local function normalize(im,size,maxw,maxh,baseline,quantize,slime)
 local x0,y0,x1,y1=im.width,im.height,-1,-1
 for p in im:pixels() do if pc.rgbaA(p())>=100 then
  x0=math.min(x0,p.x);y0=math.min(y0,p.y);x1=math.max(x1,p.x);y1=math.max(y1,p.y)
 end end
 local out=Image(size,size,ColorMode.RGB)
 if x1<x0 then return out end
 local scale=math.min(maxw/(x1-x0+1),maxh/(y1-y0+1))
 local w=math.floor((x1-x0+1)*scale+.5);local h=math.floor((y1-y0+1)*scale+.5)
 local dx=math.floor((size-w)/2);local dy=baseline-h
 for y=0,h-1 do for x=0,w-1 do
  local sx=x0+math.min(x1-x0,math.floor((x+.5)*(x1-x0+1)/w))
  local sy=y0+math.min(y1-y0,math.floor((y+.5)*(y1-y0+1)/h))
  local v=im:getPixel(sx,sy)
  if pc.rgbaA(v)>=100 then
   if quantize then v=nearest(v) else v=rgba(pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v)) end
   if slime and pc.rgbaB(v)>pc.rgbaR(v)+20 and pc.rgbaG(v)>100 then
    v=rgba(pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v),210)
   end
   out:drawPixel(dx+x,dy+y,v)
  end
 end end
 return out
end
local function saveImage(im,path)
 local s=Sprite(im.width,im.height,ColorMode.RGB)
 s:newCel(s.layers[1],1,im,Point(0,0));s:saveAs(root..'/'..path);s:close()
end
local function sheet(images,name,size)
 local im=Image(size*#images,size,ColorMode.RGB)
 for i,v in ipairs(images) do im:drawImage(v,Point((i-1)*size,0)) end
 saveImage(im,'sprites/'..name..'.png')
end
local function put(im,x,y,v) if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,v) end end
local function part(im,x0,y0,x1,y1,dx,dy)
 local out=Image(im.width,im.height,ColorMode.RGB)
 for y=y0,y1 do for x=x0,x1 do local v=im:getPixel(x,y)
  if pc.rgbaA(v)>0 then put(out,x+dx,y+dy,v) end
 end end
 return out
end

-- Remove generated grayscale checkerboard, then sample into a coherent palette.
local chefSrc=openImage('chef');local half=math.floor(chefSrc.width/2)
local dirs={};local dirNames={'south','east','north','west'}
for i=1,4 do
 local x=((i-1)%2)*half;local y=math.floor((i-1)/2)*half
 dirs[i]=normalize(extract(chefSrc,x,y,half,half,true),64,34,50,58,true,false)
 saveImage(dirs[i],'sprites/chef_idle_'..dirNames[i]..'.png')
end
sheet(dirs,'chef_directions',64)

-- Aseprite reconstruction: stable head/torso, separate feet and near casting arm.
-- These are blockout animation drafts, not independently regenerated frames.
local s=Sprite(64,64,ColorMode.RGB)
s.layers[1].name='FarBoot';local far=s.layers[1]
local body=s:newLayer();body.name='HeadAndTorso'
local near=s:newLayer();near.name='NearBoot'
local arm=s:newLayer();arm.name='NearArm'
for i=2,11 do s:newFrame() end
for i=1,4 do s:newCel(body,i,dirs[i],Point(0,0)) end
local east=dirs[2]
local feetY=51
for f=1,4 do
 local frame=f+4;local stride=({-2,0,2,0})[f];local bob=({0,-1,0,-1})[f]
 s:newCel(body,frame,part(east,0,0,63,feetY-1,0,bob),Point(0,0))
 s:newCel(far,frame,part(east,32,feetY,63,63,-stride,0),Point(0,0))
 s:newCel(near,frame,part(east,0,feetY,31,63,stride,0),Point(0,0))
 s.frames[frame].duration=.12
end
for f=1,3 do
 local base=Image(east)
 -- Remove the foreground hanging arm, patch with robe colors before extending it.
 for y=35,47 do for x=28,34 do
  if pc.rgbaA(base:getPixel(x,y))>0 then base:drawPixel(x,y,rgba(81,65,95)) end
 end end
 s:newCel(body,f+8,base,Point(0,0))
 local armImage=Image(64,64,ColorMode.RGB)
 local cy=({39,34,38})[f];local reach=({10,15,11})[f]
 -- Small deliberately authored stepped sleeve and hand; body and costume stay fixed.
 for x=31,31+reach do
  local mid=math.floor(36+(cy-36)*(x-31)/reach+.5)
  for dy=-2,3 do
   local c=rgba(61,48,64)
   if dy>-2 and dy<3 then c=x<31+reach-3 and rgba(111,61,86) or rgba(255,174,114) end
   armImage:drawPixel(x,mid+dy,c)
  end
 end
 s:newCel(arm,f+8,armImage,Point(0,0));s.frames[f+8].duration=({.14,.10,.18})[f]
end
for i,n in ipairs(dirNames) do local t=s:newTag(i,i);t.name='idle_'..n end
local walk=s:newTag(5,8);walk.name='walk_east';local cast=s:newTag(9,11);cast.name='cast_east'
s:saveAs(root..'/sprites/chef_blockout.aseprite')
local all={};local walks={};local casts={}
for i=1,11 do local im=Image(s.spec);im:drawSprite(s,i,Point(0,0));all[i]=im
 if i>=5 and i<=8 then walks[#walks+1]=im;saveImage(im,'sprites/chef_walk_east_'..(i-4)..'.png') end
 if i>=9 then casts[#casts+1]=im;saveImage(im,'sprites/chef_cast_east_'..(i-8)..'.png') end
end
sheet(all,'chef_all',64);sheet(walks,'chef_walk_east',64);sheet(casts,'chef_cast_east',64);s:close()

local bread=openImage('bread');local cw=math.floor(bread.width/3);local foods={}
for i,n in ipairs({'raw','baked','burnt'}) do
 -- Retain a shared crop and scale instead of inflating each state to identical height.
 local cell=extract(bread,(i-1)*cw,0,cw,bread.height,false)
 foods[i]=normalize(cell,32,26,19,26,true,false)
 if i~=2 then
  -- Keep footprint width; dough is low, burnt bread has a collapsed crown.
  local flat=Image(32,32,ColorMode.RGB);local height=i==1 and 15 or 14
  for y=0,height-1 do for x=0,31 do
   flat:drawPixel(x,26-height+y,foods[i]:getPixel(x,7+math.floor((y+.5)*19/height)))
  end end
  foods[i]=flat
 end
 saveImage(foods[i],'sprites/bread_'..n..'.png')
end
sheet(foods,'bread_states',32)
local slime=normalize(openImage('slime'),32,23,25,28,true,true)
saveImage(slime,'sprites/slime_idle.png')
local slimeS=Sprite(32,32,ColorMode.RGB);slimeS.layers[1].name='WaterBody'
slimeS:newCel(slimeS.layers[1],1,slime,Point(0,0));slimeS:saveAs(root..'/sprites/slime.aseprite');slimeS:close()

for _,kind in ipairs({'fire','ice'}) do
 local src=openImage(kind);local w=math.floor(src.width/2);local effects={}
 for i,state in ipairs({'normal','accident'}) do
  local cropY=(kind=='ice' and i==1) and math.floor(src.height*.62) or 0
  local im=normalize(extract(src,(i-1)*w,cropY,w,src.height-cropY,false),32,30,i==1 and 10 or 24,29,true,false)
  -- Guarantee the central food-readability window is truly transparent.
  for y=6,22 do for x=10,21 do im:drawPixel(x,y,0) end end
  effects[i]=im;saveImage(im,'sprites/'..kind..'_'..state..'.png')
 end
 sheet(effects,kind..'_states',32)
end

-- Downsample the generated material atlas in Aseprite, then make edge-safe swatches.
local atlas=openImage('atlas');local atlas128=Image(128,128,ColorMode.RGB)
for y=0,127 do for x=0,127 do
 atlas128:drawPixel(x,y,atlas:getPixel(math.floor((x+.5)*atlas.width/128),math.floor((y+.5)*atlas.height/128)))
end end
saveImage(atlas128,'modules/material_atlas_128.png')
for i,name in ipairs({'ceramic','wood','terracotta','sage'}) do
 local ox=((i-1)%2)*64;local oy=math.floor((i-1)/2)*64
 local im=extract(atlas128,ox,oy,64,64,false)
 local bases={{244,229,198},{173,113,60},{185,84,62},{141,155,114}};local c=bases[i]
 for y=0,63 do for x=0,63 do
  local v=im:getPixel(x,y);local edge=math.min(x,y,63-x,63-y)
  -- Calm down AI tonal gradients; make opposing 4-pixel borders identical.
  local weight=edge<4 and 0 or .30
  im:drawPixel(x,y,rgba(math.floor(c[1]*(1-weight)+pc.rgbaR(v)*weight),math.floor(c[2]*(1-weight)+pc.rgbaG(v)*weight),math.floor(c[3]*(1-weight)+pc.rgbaB(v)*weight)))
 end end
 saveImage(im,'modules/'..name..'_64.png')
end

-- Exact 16:9 concepts: crop only two bottom rows, no aspect distortion.
for _,name in ipairs({'A','B'}) do
 local src=openImage('concept_'..name)
 local im=Image(1664,936,ColorMode.RGB)
 im:drawImage(src,Point(-4,-2));saveImage(im,'concepts/concept_'..name..'.png')
end
local preview=Image(512,192,ColorMode.RGB)
local names={'chef_idle_south','chef_idle_east','chef_idle_north','chef_idle_west',
 'chef_walk_east_1','chef_walk_east_2','chef_walk_east_3','chef_walk_east_4',
 'chef_cast_east_1','chef_cast_east_2','chef_cast_east_3','bread_raw','bread_baked','bread_burnt','slime_idle',
 'fire_normal','fire_accident','ice_normal','ice_accident'}
for i,name in ipairs(names) do
 local x=((i-1)%8)*64;local y=math.floor((i-1)/8)*64
 local bg=i%2==0 and rgba(166,55,123) or rgba(47,42,52)
 for py=y,y+63 do for px=x,x+63 do preview:drawPixel(px,py,bg) end end
 local sp=app.open(root..'/sprites/'..name..'.png');local im=Image(sp.spec);im:drawSprite(sp,1,Point(0,0))
 preview:drawImage(im,Point(x+math.floor((64-im.width)/2),y+math.floor((64-im.height)/2)));sp:close()
end
saveImage(preview,'qa/sprite_contact.png')
print('ART_OK')
