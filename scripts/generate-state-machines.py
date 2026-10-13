"""Generate editable state machines from the reviewed documentation inventory."""
from pathlib import Path
import json
import xml.etree.ElementTree as ET

BASE = Path(__file__).resolve().parents[1] / 'docs' / '10-statemachine'

def add_final_states(doc, model):
    """Append explicit UML completion nodes without changing existing cells."""
    graph = doc.find('./diagram/mxGraphModel')
    root = graph.find('root')
    cells = {cell.get('id'): cell for cell in root.findall('mxCell')}
    for state in model.get('final_states', []):
        if state not in model['states']:
            raise ValueError(f'Unknown final source: {state}')
        if any(edge[0] == state for edge in model['edges']):
            raise ValueError(f'Final source still has outgoing transitions: {state}')
        geometry = cells[state].find('mxGeometry')
        x, y = float(geometry.get('x')), float(geometry.get('y'))
        lowest = max(float(cells[key].find('mxGeometry').get('y'))
                     for key in model['states'])
        below = y == lowest
        fx, fy = (x + 115, y + 140) if below else (x + 340, y + 25)
        final_id = f'final-{state}'
        cell = ET.SubElement(root, 'mxCell', id=final_id, value='', vertex='1',
                             parent='1', style='ellipse;html=1;shape=endState;'
                             'fillColor=#1e293b;strokeColor=#1e293b;strokeWidth=2;')
        ET.SubElement(cell, 'mxGeometry', x=f'{fx:g}', y=f'{fy:g}', width='30',
                      height='30', attrib={'as': 'geometry'})
        pins = ('exitX=0.5;exitY=1;entryX=0.5;entryY=0;' if below else
                'exitX=1;exitY=0.5;entryX=0;entryY=0.5;')
        edge = ET.SubElement(root, 'mxCell', id=f'completion-{state}', value='',
                             edge='1', parent='1', source=state, target=final_id,
                             style='edgeStyle=orthogonalEdgeStyle;rounded=1;'
                             'orthogonalLoop=1;jettySize=auto;html=1;endArrow=open;'
                             'endSize=10;strokeWidth=2;strokeColor=#334155;' + pins)
        ET.SubElement(edge, 'mxGeometry', relative='1', attrib={'as': 'geometry'})
        height = max(float(graph.get('pageHeight')), fy + 90)
        graph.set('pageHeight', f'{height:g}')
        graph.set('dy', f'{height:g}')

def generate(model):
    name = model['id']
    doc = ET.Element('mxfile', host='app.diagrams.net', version='24.7.17')
    page = ET.SubElement(doc, 'diagram', id=name, name=model['title'])
    height = max(model.get('positions', {}).get(state, [460, 220 + 180 * index])[1]
                 for index, state in enumerate(model['states'])) + 80
    graph = ET.SubElement(page, 'mxGraphModel', dx='1200', dy=str(height), grid='1', gridSize='10', page='1', pageScale='1', pageWidth='1200', pageHeight=str(height), background='#ffffff')
    root = ET.SubElement(graph, 'root')
    ET.SubElement(root, 'mxCell', id='0')
    ET.SubElement(root, 'mxCell', id='1', parent='0')
    def vertex(id, text, x, y, w, h, style):
        cell = ET.SubElement(root, 'mxCell', id=id, value=text, vertex='1', parent='1', style='html=1;whiteSpace=wrap;fontFamily=Arial;fontSize=16;'+style+';')
        ET.SubElement(cell, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), attrib={'as':'geometry'})
    vertex('title', model['title'], 40, 20, 1120, 50, 'text;align=center;fontStyle=1;fontSize=24')
    vertex('initial', '', 580, 80, 20, 20, 'ellipse;fillColor=#1e293b;strokeColor=#1e293b')
    positions = {'initial':(590,90)}
    for index, state in enumerate(model['states']):
        key = state
        y = 220 + 180 * index
        x,y = model.get('positions',{}).get(key,[460,y])
        y -= 60
        positions[key] = (x+130,y+40)
        vertex(key, key, x, y, 260, 80, 'rounded=1;arcSize=20;fillColor=#ffffff;strokeColor=#334155;fontColor=#172554;strokeWidth=2')
    edges = [('initial',model['states'][0],model['initial'])]+[tuple(edge) for edge in model['edges']]
    for index, (a,b,label,*rest) in enumerate(edges):
        delta = positions[b][1]-positions[a][1]
        direct = delta > 0 and delta <= 180
        side = 1 if index % 2 else 0
        style = 'edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=open;endSize=10;strokeWidth=2;strokeColor=#334155;fontColor=#334155;fontFamily=Arial;fontSize=14;labelBackgroundColor=#ffffff;'
        route = model.get('routes',{}).get(str(index))
        if route:
            ex,ey,tx,ty = route['pins']
            style += f'exitX={ex};exitY={ey};entryX={tx};entryY={ty};'
            style += route.get('style','')
        elif direct:
            style += 'exitX=0.5;exitY=1;entryX=0.5;entryY=0;'
        else:
            style += f'exitX={side};exitY=0.3;entryX={side};entryY=0.7;'
        cell = ET.SubElement(root,'mxCell',id=f'transition-{index}',value=label,edge='1',parent='1',source=a,target=b,style=style)
        geom = ET.SubElement(cell,'mxGeometry',relative='1',attrib={'as':'geometry'})
        if route:
            pts = ET.SubElement(geom,'Array',attrib={'as':'points'})
            for x,y in route['points']:
                ET.SubElement(pts,'mxPoint',x=str(x),y=str(y-60))
        elif not direct:
            lane = (800+40*(index%4)) if side else (380-40*(index%4))
            pts = ET.SubElement(geom,'Array',attrib={'as':'points'})
            ET.SubElement(pts,'mxPoint',x=str(lane),y=str(positions[a][1]-16))
            ET.SubElement(pts,'mxPoint',x=str(lane),y=str(positions[b][1]+16))
    add_final_states(doc, model)
    ET.indent(doc)
    ET.ElementTree(doc).write(BASE / (name+'.drawio'),encoding='utf-8',xml_declaration=True)

if __name__ == '__main__':
    models = json.loads((BASE/'state-machines.json').read_text(encoding='utf-8'))
    for model in models:
        generate(model)
    print(f'Generated {len(models)} state machines in {BASE}')
