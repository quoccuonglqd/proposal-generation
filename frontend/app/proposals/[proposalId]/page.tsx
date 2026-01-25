"use client";

import React, { useEffect, useState } from 'react';
import {
    Box,
    Container,
    Typography,
    Paper,
    Chip,
    Button,
    Stack,
    Divider,
    List,
    ListItem,
    ListItemText,
    ListItemIcon,
    CircularProgress
} from '@mui/material';
import {
    Description as PptxIcon,
    PictureAsPdf as PdfIcon,
    CheckCircle as SuccessIcon,
    Pending as PendingIcon
} from '@mui/icons-material';
import { proposalApi } from '../../services/api';
import { useParams } from 'next/navigation';

const getAbsoluteUrl = (url: string) => {
    if (url.startsWith('http')) return url;
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5008/api/v1';
    // Remove /api/v1 from end to get domain
    const domain = baseUrl.replace(/\/api\/v1\/?$/, '');
    return `${domain}${url}`;
};

export default function ProposalDetail() {
    const { proposalId } = useParams();
    const [proposal, setProposal] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [user, setUser] = useState<any>(null);

    const fetchProposal = async () => {
        try {
            const res = await proposalApi.getById(proposalId as string);
            setProposal(res.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        const storedUser = localStorage.getItem('user');
        if (storedUser) setUser(JSON.parse(storedUser));
        fetchProposal();
        // Poll for status updates if generated
        const timer = setInterval(() => {
            if (proposal?.status === 'GENERATING' || proposal?.status === 'DRAFT') {
                fetchProposal();
            }
        }, 5000);
        return () => clearInterval(timer);
    }, [proposalId, proposal?.status]);

    if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;
    if (!proposal) return <Typography align="center">Proposal not found.</Typography>;

    const isAdmin = user?.roles?.includes('Admin');
    const isCreatorFull = user?.roles?.includes('ProposalCreator') || isAdmin;

    return (
        <Container maxWidth="md" sx={{ py: 8 }}>
            <Paper sx={{ p: 4 }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                    <Box>
                        <Typography variant="h4" fontWeight="bold">{proposal.clientName}</Typography>
                        <Typography color="text.secondary">ID: {proposal.id}</Typography>
                    </Box>
                    <Stack direction="row" spacing={1} alignItems="center">
                        <Chip
                            label={proposal.status}
                            color={proposal.status === 'GENERATED' ? 'success' : proposal.status === 'ERROR' ? 'error' : 'warning'}
                            icon={proposal.status === 'GENERATED' ? <SuccessIcon /> : <PendingIcon />}
                        />
                        {isCreatorFull && (proposal.status === 'DRAFT' || proposal.status === 'ERROR') && (
                            <Button variant="contained" size="small" onClick={() => proposalApi.generate(proposal.id, { language: proposal.language || 'en' })}>
                                Generate
                            </Button>
                        )}
                    </Stack>
                </Stack>

                <Divider sx={{ mb: 4 }} />

                <Box sx={{ mb: 4 }}>
                    <Typography variant="h6" gutterBottom>Selected Services</Typography>
                    <List>
                        {proposal.serviceSelections.map((s: any) => (
                            <ListItem key={s.serviceId}>
                                <ListItemText
                                    primary={s.serviceName}
                                    secondary={`Quantity: ${s.quantity} | Total: ${(s.quantity * s.price.local).toLocaleString()} VND`}
                                />
                            </ListItem>
                        ))}
                    </List>
                </Box>

                <Box sx={{ mb: 4 }}>
                    <Typography variant="h6" gutterBottom>Proposal Content</Typography>
                    {proposal.sections && proposal.sections.length > 0 ? (
                        <List>
                            {proposal.sections.map((s: any) => (
                                <ListItem key={s.key}>
                                    <ListItemText
                                        primary={s.key.replace(/_/g, ' ')}
                                        secondary={typeof s.content === 'object' ? JSON.stringify(s.content) : s.content}
                                    />
                                </ListItem>
                            ))}
                        </List>
                    ) : (
                        <Typography color="text.secondary">No sections found for this proposal.</Typography>
                    )}
                </Box>

                <Box sx={{ mb: 4 }}>
                    <Typography variant="h6" gutterBottom>Artifacts</Typography>
                    {proposal.status === 'GENERATED' ? (
                        <Stack direction="row" spacing={2}>
                            {proposal.artifacts?.map((artifact: any) => (
                                <Button
                                    key={artifact.id}
                                    variant="outlined"
                                    startIcon={artifact.type === 'PPTX' ? <PptxIcon /> : <PdfIcon />}
                                    component="a"
                                    href={getAbsoluteUrl(artifact.downloadUrl)}
                                    download={artifact.fileName}
                                >
                                    Download {artifact.type}
                                </Button>
                            ))}
                            {(!proposal.artifacts || proposal.artifacts.length === 0) && (
                                <Typography color="text.secondary">No artifacts available.</Typography>
                            )}
                        </Stack>
                    ) : (
                        <Typography color="text.secondary">Generating documents... please wait.</Typography>
                    )}
                </Box>

                <Box sx={{ textAlign: 'center', mt: 4 }}>
                    <Button variant="contained" onClick={() => window.location.href = '/'}>
                        Back to Dashboard
                    </Button>
                </Box>
            </Paper>
        </Container>
    );
}
