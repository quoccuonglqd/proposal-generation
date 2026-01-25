"use client";

import React, { useState, useEffect } from 'react';
import {
    Box,
    Typography,
    Paper,
    List,
    ListItem,
    ListItemText,
    IconButton,
    Button,
    Dialog,
    DialogTitle,
    DialogContent,
    DialogActions,
    TextField,
    Stack,
    FormControl,
    InputLabel,
    Select,
    MenuItem,
    Card,
    ListItemIcon
} from '@mui/material';
import {
    DragIndicator as DragIcon,
    Edit as EditIcon,
    Delete as DeleteIcon,
    Add as AddIcon
} from '@mui/icons-material';
import {
    DndContext,
    closestCenter,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors,
} from '@dnd-kit/core';
import {
    arrayMove,
    SortableContext,
    sortableKeyboardCoordinates,
    verticalListSortingStrategy,
    useSortable
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { proposalApi } from '../services/api';

interface Section {
    key: string;
    order: number;
    content: any;
    backgroundType: string;
    backgroundAssetId?: string;
}

interface SectionEditorProps {
    proposalId: string;
    sections: Section[];
    onChange: (sections: Section[]) => void;
}

function SortableItem({ section, onEdit }: { section: Section, onEdit: () => void }) {
    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
    } = useSortable({ id: section.key });

    const style = {
        transform: CSS.Transform.toString(transform),
        transition,
        marginBottom: '8px'
    };

    return (
        <Card ref={setNodeRef} style={style} variant="outlined">
            <ListItem
                secondaryAction={
                    <Stack direction="row" spacing={1}>
                        <IconButton onClick={onEdit}><EditIcon /></IconButton>
                    </Stack>
                }
            >
                <ListItemIcon {...attributes} {...listeners} sx={{ cursor: 'grab' }}>
                    <DragIcon />
                </ListItemIcon>
                <ListItemText
                    primary={section.key.replace(/_/g, ' ')}
                    secondary={`Order: ${section.order}`}
                />
            </ListItem>
        </Card>
    );
}

// Sortable Context Setup
export default function SectionEditor({ proposalId, sections, onChange }: SectionEditorProps) {
    const [editingSection, setEditingSection] = useState<Section | null>(null);
    const [editDialogOpen, setEditDialogOpen] = useState(false);

    const sensors = useSensors(
        useSensor(PointerSensor),
        useSensor(KeyboardSensor, {
            coordinateGetter: sortableKeyboardCoordinates,
        })
    );

    const handleDragEnd = (event: any) => {
        const { active, over } = event;

        if (active.id !== over.id) {
            const oldIndex = sections.findIndex(s => s.key === active.id);
            const newIndex = sections.findIndex(s => s.key === over.id);
            const newArray = arrayMove(sections, oldIndex, newIndex).map((s, idx) => ({
                ...s,
                order: idx + 1
            }));
            onChange(newArray);
        }
    };

    const handleEdit = (section: Section) => {
        setEditingSection({ ...section });
        setEditDialogOpen(true);
    };

    const handleSaveEdit = () => {
        if (editingSection) {
            onChange(sections.map(s => s.key === editingSection.key ? editingSection : s));
            setEditDialogOpen(false);
        }
    };

    return (
        <Box>
            <Typography variant="h6" gutterBottom>Proposal Sections</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                Drag and drop to reorder sections. Use the edit icon to configure content and backgrounds.
            </Typography>

            <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                onDragEnd={handleDragEnd}
            >
                <SortableContext
                    items={sections.map(s => s.key)}
                    strategy={verticalListSortingStrategy}
                >
                    <List>
                        {sections.map((section) => (
                            <SortableItem key={section.key} section={section} onEdit={() => handleEdit(section)} />
                        ))}
                    </List>
                </SortableContext>
            </DndContext>

            <Dialog open={editDialogOpen} onClose={() => setEditDialogOpen(false)} fullWidth maxWidth="sm">
                <DialogTitle>Edit Section: {editingSection?.key}</DialogTitle>
                <DialogContent>
                    <Box sx={{ pt: 2 }}>
                        <FormControl fullWidth sx={{ mb: 2 }}>
                            <InputLabel>Background Type</InputLabel>
                            <Select
                                value={editingSection?.backgroundType || 'None'}
                                label="Background Type"
                                onChange={(e) => setEditingSection(prev => prev ? {
                                    ...prev,
                                    backgroundType: e.target.value
                                } : null)}
                            >
                                <MenuItem value="None">None</MenuItem>
                                <MenuItem value="Color">Color</MenuItem>
                                <MenuItem value="Image">Image</MenuItem>
                            </Select>
                        </FormControl>

                        <Typography variant="subtitle1" gutterBottom>Content (JSON Editor)</Typography>
                        <TextField
                            fullWidth
                            multiline
                            rows={6}
                            value={JSON.stringify(editingSection?.content || {}, null, 2)}
                            onChange={(e) => {
                                try {
                                    const content = JSON.parse(e.target.value);
                                    setEditingSection(prev => prev ? { ...prev, content } : null);
                                } catch (err) {
                                    // Handle JSON error silently while typing
                                }
                            }}
                            helperText="Enter section content as JSON (e.g. { 'en': { 'title': 'Why Us' } })"
                        />
                    </Box>
                </DialogContent>
                <DialogActions>
                    <Button onClick={() => setEditDialogOpen(false)}>Cancel</Button>
                    <Button onClick={handleSaveEdit} variant="contained">Save Changes</Button>
                </DialogActions>
            </Dialog>
        </Box>
    );
}
